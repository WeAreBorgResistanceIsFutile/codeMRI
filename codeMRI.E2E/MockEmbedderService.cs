using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.E2E;

/// <summary>
/// Mock embedder service for testing that doesn't require Ollama
/// </summary>
public class MockEmbedderService : IEmbedder
{
    public Task<float[]> EmbedAsync(string text)
    {
        // Return a mock embedding vector with the expected dimension (768)
        return Task.FromResult(new float[768]); // Qdrant expects 768-dimensional vectors
    }

    public Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts)
    {
        // Return mock embedding vectors for batch with the expected dimension (768)
        var result = new List<float[]>();
        foreach (var text in texts)
        {
            result.Add(new float[768]);
        }
        return Task.FromResult(result);
    }
}
