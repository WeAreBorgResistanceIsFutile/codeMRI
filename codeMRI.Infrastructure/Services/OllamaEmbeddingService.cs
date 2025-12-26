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
    private readonly string _model;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<OllamaSettings> ollamaOptions,
        IOptions<EmbeddingSettings> embeddingOptions,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = embeddingOptions.Value.Model;

        var ollamaSettings = ollamaOptions.Value;
        if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(ollamaSettings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(ollamaSettings.BaseUrl);
        }
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", new
        {
            model = _model,
            prompt = text
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            throw new HttpRequestException(
                $"Response status code does not indicate success: {statusCode} ({response.ReasonPhrase}). Error: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        
        if (result?.Embedding == null || result.Embedding.Length == 0)
        {
            _logger.LogError("Ollama returned null or empty embedding for model {Model}", _model);
            throw new InvalidOperationException($"Ollama returned null or empty embedding for model {_model}");
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

