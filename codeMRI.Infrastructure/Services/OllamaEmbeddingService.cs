using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using codeMRI.Infrastructure.Configuration;

namespace codeMRI.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly OllamaSettings _options;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<OllamaSettings> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", new
        {
            model = _options.EmbeddingModel,
            prompt = text
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        
        if (result?.Embedding == null || result.Embedding.Length == 0)
        {
            _logger.LogError("Ollama returned null or empty embedding for model {Model}", _options.EmbeddingModel);
            throw new InvalidOperationException($"Ollama returned null or empty embedding for model {_options.EmbeddingModel}");
        }

        return result.Embedding;
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

