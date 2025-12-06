using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Document = codeMRI.Core.Models.Document;

namespace codeMRI.Infrastructure.Services;

public class QdrantVectorDb : IVectorDatabase
{
    private readonly IQdrantClient _client;
    private readonly QdrantSettings _settings;
    private string _collectionName = "coremri_docs";

    public QdrantVectorDb(IQdrantClient client, IOptions<QdrantSettings> settings)
    {
        _settings = settings.Value;
        _client = client;
    }

    public async Task InitializeAsync(string collectionName)
    {
        _collectionName = collectionName;
        var collections = await _client.ListCollectionsAsync();
        if (collections.All(c => c != _collectionName))
            await _client.CreateCollectionAsync(_collectionName,
                new VectorParams { Size = (ulong)_settings.VectorSize, Distance = Distance.Cosine });
    }

    public async Task UpsertAsync(IEnumerable<Document> documents)
    {
        var points = new List<PointStruct>();
        foreach (var doc in documents)
        {
            if (doc.Embedding == null) continue;

            var payload = new Dictionary<string, object>
            {
                { "content", doc.Content },
                { "file_path", doc.FilePath }
            };

            foreach (var kvp in doc.Metadata) payload[kvp.Key] = kvp.Value;

            // Ensure ID is a Guid or convert it safely. coreMRI uses Guid strings.
            // Qdrant supports UUIDs.
            var id = Guid.TryParse(doc.Id, out var guid) ? guid : Guid.NewGuid();

            var point = new PointStruct
            {
                Id = id,
                Vectors = doc.Embedding
            };

            foreach (var kvp in payload) point.Payload.Add(kvp.Key, ConvertToValue(kvp.Value));

            points.Add(point);
        }

        if (points.Any()) await _client.UpsertAsync(_collectionName, points);
    }

    public async Task<IEnumerable<Document>> SearchAsync(float[] vector, int topK = 20)
    {
        var results = await _client.SearchAsync(_collectionName, vector, limit: (ulong)topK);

        return results.Select(s => new Document
        {
            Id = s.Id.Uuid.ToString(),
            Content = s.Payload.TryGetValue("content", out var contentVal) ? contentVal.StringValue : string.Empty,
            FilePath = s.Payload.TryGetValue("file_path", out var pathVal) ? pathVal.StringValue : string.Empty,
            Embedding = null, // Optimization: don't return vector unless needed
            Metadata = s.Payload.ToDictionary(
                k => k.Key,
                v => v.Value.KindCase == Value.KindOneofCase.StringValue ? v.Value.StringValue : v.Value.ToString())
        });
    }

    public async Task DeleteByMetadataAsync(string key, string value)
    {
        // Create a filter to match the payload key/value
        var filter = new Filter
        {
            Must = { new Condition { Field = new FieldCondition { Key = key, Match = new Match { Keyword = value } } } }
        };

        await _client.DeleteAsync(_collectionName, filter);
    }

    private Value ConvertToValue(object? obj)
    {
        if (obj == null) return new Value { NullValue = NullValue.NullValue };
        return obj switch
        {
            string s => s,
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            bool b => b,
            _ => obj.ToString() ?? ""
        };
    }
}