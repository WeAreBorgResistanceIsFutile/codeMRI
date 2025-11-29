using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

public class OllamaEmbedderService : IEmbedder
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;

    public OllamaEmbedderService(HttpClient httpClient, IOptions<OllamaSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Embedding can take time for large batches
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var request = new
        {
            model = _settings.EmbeddingModel,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }

    public async Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts)
    {
        // Ollama /api/embeddings only supports one at a time currently.
        // We will run them in parallel with a degree of parallelism control if needed, 
        // but for now, simple Task.WhenAll is fine for local use or sequential loop.
        // Note: Ollama might queue requests.
        
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await EmbedAsync(text));
        }
        return results;
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
