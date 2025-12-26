using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Grpc.Core;

namespace codeMRI.Infrastructure.Services;

public class QdrantVectorStoreService : IVectorStoreService, IDisposable
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStoreService> _logger;
    private readonly VectorStoreSettings _settings;

    public QdrantVectorStoreService(
        IOptions<VectorStoreSettings> settings,
        ILogger<QdrantVectorStoreService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new QdrantClient(_settings.Host, _settings.Port, _settings.UseHttps, _settings.ApiKey);
    }

    public async Task CreateCollectionAsync(string collectionName, int vectorSize)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();
            if (collections.Contains(collectionName))
            {
                _logger.LogInformation("Collection {CollectionName} already exists", collectionName);
                return;
            }

            await _client.CreateCollectionAsync(collectionName, new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Cosine
            });
            
            _logger.LogInformation("Created collection {CollectionName} with size {Size}", collectionName, vectorSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection {CollectionName}", collectionName);
            throw;
        }
    }

    public async Task<string> UpsertAsync(string collectionName, VectorDocument document)
    {
        try
        {
            var point = new PointStruct
            {
                Id = Guid.Parse(document.Id), // Qdrant IDs must be UUID or uint
                Vectors = document.Vector
            };

            point.Payload.Add("text", document.Text);
            foreach (var kvp in document.Metadata)
            {
                point.Payload.Add(kvp.Key, kvp.Value.ToString() ?? string.Empty);
            }

            await _client.UpsertAsync(collectionName, new[] { point });
            return document.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert document {DocumentId} to collection {CollectionName}", document.Id, collectionName);
            throw;
        }
    }

    public async Task<List<ScoredDocument>> SearchAsync(
        string collectionName, 
        float[] queryVector, 
        int topK, 
        Dictionary<string, object>? filter = null)
    {
        try
        {
            Filter? qdrantFilter = null;
            if (filter != null && filter.Any())
            {
                qdrantFilter = new Filter();
                foreach (var kvp in filter)
                {
                    qdrantFilter.Must.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = kvp.Key,
                            Match = new Match { Keyword = kvp.Value.ToString() }
                        }
                    });
                }
            }

            var results = await _client.SearchAsync(
                collectionName, 
                queryVector, 
                filter: qdrantFilter,
                limit: (ulong)topK);

            return results.Select(r => new ScoredDocument
            {
                Score = r.Score,
                Document = new VectorDocument
                {
                    Id = r.Id.Uuid ?? r.Id.Num.ToString(),
                    Text = r.Payload.GetValueOrDefault("text")?.StringValue ?? string.Empty,
                    Metadata = r.Payload.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.StringValue)
                }
            }).ToList();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogDebug("Collection {CollectionName} not found, returning empty results", collectionName);
            return new List<ScoredDocument>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search in collection {CollectionName}", collectionName);
            return new List<ScoredDocument>();
        }
    }

    public async Task DeleteCollectionAsync(string collectionName)
    {
        try
        {
            await _client.DeleteCollectionAsync(collectionName);
            _logger.LogInformation("Deleted collection {CollectionName}", collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection {CollectionName}", collectionName);
        }
    }

    public async Task DeleteByFilterAsync(string collectionName, Dictionary<string, object> filter)
    {
        try
        {
            if (filter == null || !filter.Any()) return;

            var qdrantFilter = new Filter();
            foreach (var kvp in filter)
            {
                qdrantFilter.Must.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = kvp.Key,
                        Match = new Match { Keyword = kvp.Value.ToString() }
                    }
                });
            }

            // Qdrant's DeleteAsync takes a points-selector. We use the filter-selector.
            await _client.DeleteAsync(collectionName, qdrantFilter);
            _logger.LogInformation("Deleted points from {CollectionName} matching filter", collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from collection {CollectionName} with filter", collectionName);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
