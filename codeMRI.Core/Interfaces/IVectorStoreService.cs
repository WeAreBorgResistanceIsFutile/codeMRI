using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
/// Service for interacting with a vector database.
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Ensures a collection exists with the specified vector dimensions.
    /// </summary>
    Task CreateCollectionAsync(string collectionName, int vectorSize);

    /// <summary>
    /// Upserts a document into the vector store.
    /// </summary>
    Task<string> UpsertAsync(string collectionName, VectorDocument document);

    /// <summary>
    /// Performs a similarity search in the specified collection.
    /// </summary>
    Task<List<ScoredDocument>> SearchAsync(
        string collectionName, 
        float[] queryVector, 
        int topK, 
        Dictionary<string, object>? filter = null);

    /// <summary>
    /// Deletes documents from the vector store based on a filter.
    /// </summary>
    Task DeleteByFilterAsync(string collectionName, Dictionary<string, object> filter);

    /// <summary>
    /// Deletes a collection from the vector store.
    /// </summary>
    Task DeleteCollectionAsync(string collectionName);
}
