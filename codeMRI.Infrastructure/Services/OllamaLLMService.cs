using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

public class OllamaLLMService : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;

    public OllamaLLMService(HttpClient httpClient, IOptions<OllamaSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(10); // Generation can be slow
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, List<ChatMessage> history)
    {
        var messages = BuildMessages(systemPrompt, userPrompt, history);

        var request = new
        {
            model = _settings.ChatModel,
            messages,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return result?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string systemPrompt, string userPrompt,
        List<ChatMessage> history)
    {
        var messages = BuildMessages(systemPrompt, userPrompt, history);

        var request = new
        {
            model = _settings.ChatModel,
            messages,
            stream = true
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = content
        };

        // Use SendAsync with HttpCompletionOption.ResponseHeadersRead to start reading stream immediately
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
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

    private List<object> BuildMessages(string systemPrompt, string userPrompt, List<ChatMessage> history)
    {
        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt)) messages.Add(new { role = "system", content = systemPrompt });

        if (history != null)
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });

        messages.Add(new { role = "user", content = userPrompt });
        return messages;
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