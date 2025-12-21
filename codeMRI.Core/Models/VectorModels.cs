using System;
using System.Collections.Generic;

namespace codeMRI.Core.Models;

/// <summary>
/// Represents a document stored in a vector database.
/// </summary>
public class VectorDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public float[] Vector { get; set; } = Array.Empty<float>();
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a search result from a vector database with a similarity score.
/// </summary>
public class ScoredDocument
{
    public VectorDocument Document { get; set; } = new();
    public double Score { get; set; }
}
