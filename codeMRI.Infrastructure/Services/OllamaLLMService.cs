using System.Net;
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

    public int ContextSize => _settings.ContextSize;

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
            stream = false,
            options = new
            {
                temperature = _settings.Temperature,
                num_ctx = _settings.ContextSize
            }
        };

        var requestJson = JsonSerializer.Serialize(request);
        var displayJson = requestJson.Length > 2000 
            ? requestJson.Substring(0, 2000) + $"... [TRUNCATED {requestJson.Length - 2000} chars]" 
            : requestJson;
        
        _logger.LogInformation("Sending ChatAsync request to {Url}. Body: {Body}", "/api/chat", displayJson);

        const int maxRetries = 3;
        const int baseDelayMs = 1000; // Base delay of 1 second
        const int maxDelayMs = 5000;  // Maximum delay of 5 seconds
        var random = new Random();

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
                
                if (IsTransientError(response.StatusCode) && attempt < maxRetries)
                {
                    // Calculate delay with exponential backoff + random jitter
                    var exponentialDelay = baseDelayMs * (1 << (attempt - 1)); // 1s, 2s, 4s
                    var jitter = random.Next(0, exponentialDelay / 2); // Add random jitter up to 50% of delay
                    var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                    
                    _logger.LogWarning("Transient error {StatusCode} on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                        response.StatusCode, attempt, maxRetries, delay);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var contentString = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    if (attempt < maxRetries)
                    {
                        var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                        var jitter = random.Next(0, exponentialDelay / 2);
                        var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                        
                        _logger.LogWarning("Ollama returned an empty response on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                            attempt, maxRetries, delay);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    _logger.LogError("Ollama returned an empty response for ChatAsync after {MaxRetries} attempts. Status Code: {StatusCode}", maxRetries, response.StatusCode);
                    throw new HttpRequestException($"Ollama returned an empty response after {maxRetries} attempts. Status Code: {response.StatusCode}");
                }

                OllamaChatResponse? result;
                try
                {
                    result = JsonSerializer.Deserialize<OllamaChatResponse>(contentString);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Ollama response: {Content}", 
                        contentString.Length > 1000 ? contentString.Substring(0, 1000) + "..." : contentString);
                    throw;
                }

                var responseContent = result?.Message?.Content ?? string.Empty;
                
                _logger.LogInformation("Received ChatAsync response. Content length: {Length}", responseContent.Length);
                _logger.LogInformation("ChatAsync Response Content: {Content}", responseContent);
                
                return responseContent;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue && IsTransientError(ex.StatusCode.Value) && attempt < maxRetries)
            {
                 // Calculate delay with exponential backoff + random jitter
                 var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                 var jitter = random.Next(0, exponentialDelay / 2);
                 var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                 
                 _logger.LogWarning(ex, "HTTP Request failed with {StatusCode} on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                     ex.StatusCode, attempt, maxRetries, delay);
                 await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxRetries)
            {
                // Calculate delay with exponential backoff + random jitter for timeout retries
                var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                var jitter = random.Next(0, exponentialDelay / 2);
                var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                
                _logger.LogWarning("ChatAsync request timed out on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                    attempt, maxRetries, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("ChatAsync operation canceled or timed out after {MaxRetries} attempts.", maxRetries);
                throw;
            }
        }
        
        _logger.LogError("Max retries ({MaxRetries}) exceeded for Ollama API. Stopping retry attempts.", maxRetries);
        throw new HttpRequestException($"Max retries ({maxRetries}) exceeded for Ollama API. The service may be experiencing issues.");
    }

    public async Task<string> ChatWithFindingsAsync(string systemPrompt, string basePrompt, string largeContent,
        string? model = null, CancellationToken cancellationToken = default)
    {
        // Calculate chunk size (approx 60% of context size to leave room for findings and prompts)
        int maxTokens = _settings.ContextSize;
        int chunkTokens = (int)(maxTokens * 0.6);
        int chunkSize = chunkTokens * 4; // Approx 4 chars per token
        int overlap = chunkSize / 10;    // 10% overlap

        var chunks = codeMRI.Core.Utils.TextSplitter.Split(largeContent, chunkSize, overlap);
        string findings = string.Empty;

        _logger.LogInformation("Processing large content in {Count} chunks.", chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            // Ensure findings don't eat more than 30% of the context
            int maxFindingsChars = (int)(_settings.ContextSize * 3.5 * 0.3);
            if (findings.Length > maxFindingsChars)
            {
                _logger.LogWarning("Findings too large ({Length} chars). Truncating to {Max} chars.", findings.Length, maxFindingsChars);
                findings = "... [OLDER FINDINGS TRUNCATED]\n" + findings.Substring(findings.Length - maxFindingsChars);
            }

            var userPrompt = codeMRI.Core.Services.PromptTemplates.ChunkedFindingsPrompt(i, chunks.Count, findings, chunks[i]);
            var combinedUserPrompt = basePrompt + "\n\n" + userPrompt;

            _logger.LogInformation("Processing chunk {Index} of {Total}...", i + 1, chunks.Count);
            
            findings = await ChatAsync(systemPrompt, combinedUserPrompt, new List<ChatMessage>(), model, cancellationToken);
        }

        return findings;
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
            stream = true,
            options = new
            {
                temperature = _settings.Temperature,
                num_ctx = _settings.ContextSize
            }
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        var displayJson = jsonRequest.Length > 2000 
            ? jsonRequest.Substring(0, 2000) + $"... [TRUNCATED {jsonRequest.Length - 2000} chars]" 
            : jsonRequest;
            
        _logger.LogInformation("Starting ChatStreamAsync to {Url}. Body: {Body}", "/api/chat", displayJson);
        
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        const int maxDelayMs = 5000;
        var random = new Random();
        
        HttpResponseMessage? response = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // We need to recreate the request message for each attempt because it gets disposed
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
                {
                    Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                };

                response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (IsTransientError(response.StatusCode) && attempt < maxRetries)
                {
                    // Calculate delay with exponential backoff + random jitter
                    var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                    var jitter = random.Next(0, exponentialDelay / 2);
                    var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                    
                    _logger.LogWarning("Ollama returned {StatusCode} on stream attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                        response.StatusCode, attempt, maxRetries, delay);
                    response.Dispose();
                    response = null;
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }
                
                response.EnsureSuccessStatusCode();
                break; // Success, exit loop
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue && IsTransientError(ex.StatusCode.Value) && attempt < maxRetries)
            {
                 var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                 var jitter = random.Next(0, exponentialDelay / 2);
                 var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                 
                 _logger.LogWarning(ex, "HTTP Stream Request failed with {StatusCode} on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                     ex.StatusCode, attempt, maxRetries, delay);
                 await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxRetries)
            {
                var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                var jitter = random.Next(0, exponentialDelay / 2);
                var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);
                
                _logger.LogWarning("ChatStreamAsync request timed out on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                    attempt, maxRetries, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        if (response == null)
        {
            _logger.LogError("Max retries ({MaxRetries}) exceeded for Ollama Streaming API. Stopping retry attempts.", maxRetries);
            throw new HttpRequestException($"Max retries ({maxRetries}) exceeded for Ollama Streaming API. The service may be experiencing issues.");
        }

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
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to parse streaming JSON line: {Line}", 
                    line.Length > 200 ? line.Substring(0, 200) + "..." : line);
            }

            if (update?.Message?.Content != null) yield return update.Message.Content;

            if (update?.Done == true) break;
        }
    }

    private List<object> BuildMessagesWithContextWindow(string? systemPrompt, string? userPrompt, List<ChatMessage> history)
    {
        // Reserve space for response and overhead
        const int ResponseBuffer = 2048; // Increased from 1024 to be safer for larger responses
        int availableTokens = Math.Max(_settings.ContextSize - ResponseBuffer, 512); 
        
        // Conservative character-to-token ratio for technical text (Mix of code and prose)
        const double CharsPerToken = 4.0; 
        int maxChars = (int)(availableTokens * CharsPerToken);

        // 1. Calculate compulsory content length (System + User)
        int systemLen = systemPrompt?.Length ?? 0;
        int originalUserLen = userPrompt?.Length ?? 0;
        int compulsoryLen = systemLen + originalUserLen;

        string finalUserPrompt = userPrompt ?? string.Empty;

        if (compulsoryLen > maxChars)
        {
            int budgetForUser = maxChars - systemLen;
            if (budgetForUser < 500) budgetForUser = 500; // Absolute minimum to avoid total loss if system prompt is massive

            _logger.LogWarning("Prompt size ({Compulsory} chars) exceeds estimated context character limit ({Max} chars, ContextSize: {Tokens}). Truncating user prompt.", 
                compulsoryLen, maxChars, _settings.ContextSize);
            
            if (originalUserLen > budgetForUser)
            {
                if (budgetForUser < originalUserLen / 2)
                {
                    _logger.LogWarning("Severe truncation: more than 50% of user prompt content will be lost ({Original} -> {Truncated} chars).", 
                        originalUserLen, budgetForUser);
                }
                finalUserPrompt = (userPrompt ?? string.Empty).Substring(0, budgetForUser) + "\n\n... [CONTENT TRUNCATED DUE TO CONTEXT LIMIT]";
            }
        }

        // 2. Add History if space remains (calculate with finalized user prompt)
        int currentChars = (systemPrompt?.Length ?? 0) + finalUserPrompt.Length;
        var finalHistory = new List<ChatMessage>();

        if (history != null && history.Any())
        {
            // Iterate backwards (Newest -> Oldest)
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                int msgLen = (msg.Content?.Length ?? 0) + 50; // Add overhead
                
                if (currentChars + msgLen <= maxChars)
                {
                    finalHistory.Insert(0, msg);
                    currentChars += msgLen;
                }
                else
                {
                    _logger.LogInformation("Context window character limit reached. History truncated. Dropping {Count} older messages.", i + 1);
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
        // Conservative approximation: 1 token ~= 3.5 characters for technical text
        return (int)(text.Length / 3.5);
    }

    /// <summary>
    /// Determines if an HTTP status code represents a transient error that can be retried.
    /// </summary>
    private static bool IsTransientError(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||       // 429
        statusCode == HttpStatusCode.RequestTimeout ||        // 408
        statusCode == HttpStatusCode.ServiceUnavailable ||    // 503
        (int)statusCode >= 500;                               // 5xx

    /// <summary>
    /// Gets the retry delay, respecting Retry-After header if present, 
    /// otherwise using exponential backoff.
    /// </summary>
    private static int GetRetryDelay(HttpResponseMessage response, int baseDelayMs, int attempt)
    {
        // Check for Retry-After header (common with 429 responses)
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var retryAfter = values.FirstOrDefault();
            if (int.TryParse(retryAfter, out var seconds))
            {
                return seconds * 1000;
            }
        }
        
        // Exponential backoff: baseDelay * 2^(attempt-1)
        return baseDelayMs * (1 << (attempt - 1));
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