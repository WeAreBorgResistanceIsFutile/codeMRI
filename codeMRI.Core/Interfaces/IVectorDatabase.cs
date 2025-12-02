using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

public interface IVectorDatabase
{
    Task UpsertAsync(IEnumerable<Document> documents);
    Task<IEnumerable<Document>> SearchAsync(float[] vector, int topK = 20);
    Task InitializeAsync(string collectionName);
    Task DeleteByMetadataAsync(string key, string value);
}