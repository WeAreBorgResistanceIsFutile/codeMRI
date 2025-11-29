using System.Collections.Concurrent;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;

namespace codeMRI.Infrastructure.Services;

public class SimpleVectorDb : IVectorDatabase
{
    private readonly ConcurrentDictionary<string, Document> _documents = new();
    private string _storagePath = string.Empty;

    public async Task InitializeAsync(string collectionName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var coreMRIDir = Path.Combine(appData, "coreMRI", "Databases");
        Directory.CreateDirectory(coreMRIDir);
        
        _storagePath = Path.Combine(coreMRIDir, $"{collectionName}.json");

        if (File.Exists(_storagePath))
        {
            try 
            {
                var json = await File.ReadAllTextAsync(_storagePath);
                var docs = JsonSerializer.Deserialize<List<Document>>(json);
                if (docs != null)
                {
                    foreach (var doc in docs)
                    {
                        _documents.TryAdd(doc.Id, doc);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading database: {ex.Message}");
            }
        }
    }

    public async Task UpsertAsync(IEnumerable<Document> documents)
    {
        foreach (var doc in documents)
        {
            _documents[doc.Id] = doc;
        }
        await SaveToDiskAsync();
    }

    public Task<IEnumerable<Document>> SearchAsync(float[] vector, int topK = 20)
    {
        // Brute force cosine similarity
        var results = _documents.Values
            .Where(d => d.Embedding != null && d.Embedding.Length == vector.Length)
            .Select(d => new 
            { 
                Document = d, 
                Score = CosineSimilarity(d.Embedding!, vector) 
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Document)
            .ToList();

        return Task.FromResult((IEnumerable<Document>)results);
    }

    private async Task SaveToDiskAsync()
    {
        var docs = _documents.Values.ToList();
        var json = JsonSerializer.Serialize(docs, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_storagePath, json);
    }

    private float CosineSimilarity(float[] vecA, float[] vecB)
    {
        if (vecA.Length != vecB.Length) return 0;

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < vecA.Length; i++)
        {
            dotProduct += vecA[i] * vecB[i];
            normA += vecA[i] * vecA[i];
            normB += vecB[i] * vecB[i];
        }

        if (normA == 0 || normB == 0) return 0;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
