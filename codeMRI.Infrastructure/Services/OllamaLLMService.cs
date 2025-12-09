using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

public class OllamaLLMService : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaLLMService> _logger;

    public OllamaLLMService(HttpClient httpClient, IOptions<OllamaSettings> settings, ILogger<OllamaLLMService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        // Default to a long timeout (2 hours) to allow specific requests to control their own timeout via CancellationToken
        _httpClient.Timeout = TimeSpan.FromHours(2);
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, List<ChatMessage> history,
        string? model = null, CancellationToken cancellationToken = default)
    {
        var messages = BuildMessagesWithContextWindow(systemPrompt, userPrompt, history);

        var request = new
        {
            model = model ?? _settings.ChatModel,
            messages,
            stream = false
        };

        var requestJson = JsonSerializer.Serialize(request);
        _logger.LogInformation("Sending ChatAsync request to {Url}. Body: {Body}", "/api/chat", requestJson);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
            var responseContent = result?.Message?.Content ?? string.Empty;
            
            _logger.LogInformation("Received ChatAsync response. Content length: {Length}", responseContent.Length);
            _logger.LogDebug("ChatAsync Response Content: {Content}", responseContent);
            
            return responseContent;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ChatAsync operation canceled or timed out.");
            throw;
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string systemPrompt, string userPrompt,
        List<ChatMessage> history, string? model = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildMessagesWithContextWindow(systemPrompt, userPrompt, history);

        var request = new
        {
            model = model ?? _settings.ChatModel,
            messages,
            stream = true
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        _logger.LogInformation("Starting ChatStreamAsync to {Url}. Body: {Body}", "/api/chat", jsonRequest);
        
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatResponse? update = null;
            try
            {
                update = JsonSerializer.Deserialize<OllamaChatResponse>(line);
            }
            catch
            {
                /* Ignore parse errors */
            }

            if (update?.Message?.Content != null) yield return update.Message.Content;

            if (update?.Done == true) break;
        }
    }

    private List<object> BuildMessagesWithContextWindow(string systemPrompt, string userPrompt, List<ChatMessage> history)
    {
        // Reserve space for response and overhead
        const int ResponseBuffer = 1024; 
        int availableTokens = Math.Max(_settings.ContextSize - ResponseBuffer, 256); // Ensure minimum valid value

        // 1. Calculate compulsory tokens (System + User)
        int systemTokens = EstimateTokenCount(systemPrompt);
        int userTokens = EstimateTokenCount(userPrompt);
        int compulsory = systemTokens + userTokens;

        string finalUserPrompt = userPrompt;

        if (compulsory > availableTokens)
        {
            _logger.LogWarning("Prompt size ({Compulsory}) exceeds available context limit ({Available}). Truncating user prompt.", compulsory, availableTokens);
            
            // Limit user prompt to fit
            int budgetForUser = availableTokens - systemTokens;
            // Ensure we at least send something if system prompt is reasonable
            if (budgetForUser < 100 && systemTokens < availableTokens) budgetForUser = 100;
            
            // Truncate user prompt (approx 4 chars per token)
            int maxChars = Math.Max(0, budgetForUser * 4);
            if (finalUserPrompt.Length > maxChars)
            {
                finalUserPrompt = finalUserPrompt.Substring(0, maxChars) + "... [TRUNCATED]";
            }
        }

        // 2. Add History if space remains logic (re-calculate with finalized prompts)
        int currentTokens = EstimateTokenCount(systemPrompt) + EstimateTokenCount(finalUserPrompt);
        var finalHistory = new List<ChatMessage>();

        if (history != null && history.Any())
        {
            // Iterate backwards (Newest -> Oldest)
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                int msgTokens = EstimateTokenCount(msg.Content) + 4; // +1 token overhead approx
                
                if (currentTokens + msgTokens <= availableTokens)
                {
                    finalHistory.Insert(0, msg);
                    currentTokens += msgTokens;
                }
                else
                {
                    _logger.LogInformation("Context window full. History truncated. Dropping {Count} older messages.", i + 1);
                    break;
                }
            }
        }

        // 3. Construct Payload
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt)) 
            messages.Add(new { role = "system", content = systemPrompt });

        foreach (var msg in finalHistory)
            messages.Add(new { role = msg.Role, content = msg.Content });

        messages.Add(new { role = "user", content = finalUserPrompt });
        return messages;
    }

    private int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // Approximation: 1 token ~= 4 characters for English text
        return text.Length / 4;
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("model")] public string? Model { get; set; }

        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }

        [JsonPropertyName("message")] public MessagePart? Message { get; set; }

        [JsonPropertyName("done")] public bool Done { get; set; }
    }

    private class MessagePart
    {
        [JsonPropertyName("role")] public string? Role { get; set; }

        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}