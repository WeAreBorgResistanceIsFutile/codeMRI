using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", new
            {
                model = _options.EmbeddingModel,
                prompt = text
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            
            return result?.Embedding ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embedding from Ollama for model {Model}", _options.EmbeddingModel);
            return Array.Empty<float>();
        }
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();
        // Ollama usually supports batching implicitly by calling it multiple times or some models support it
        // For now, let's do it sequentially to be safe, but we could parallelize
        foreach (var text in texts)
        {
            embeddings.Add(await GetEmbeddingAsync(text));
        }
        return embeddings;
    }

    public int GetDimensions()
    {
        // nomic-embed-text is 768
        return 768; 
    }

    private class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}

public class OllamaOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string DocumentationModel { get; set; } = string.Empty;
    public string ChatModel { get; set; } = string.Empty;
    public int ContextSize { get; set; } = 32768;
}
