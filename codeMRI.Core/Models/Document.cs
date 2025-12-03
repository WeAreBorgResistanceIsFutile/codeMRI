using System.Text.Json.Serialization;

namespace codeMRI.Core.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();

    [JsonIgnore] // Don't always serialize embedding to client
    public float[]? Embedding { get; set; }
}