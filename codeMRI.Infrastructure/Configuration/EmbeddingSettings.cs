namespace codeMRI.Infrastructure.Configuration;

public class EmbeddingSettings
{
    public string Model { get; set; } = "nomic-embed-text";
    public int BatchSize { get; set; } = 20;
    public ChunkingSettings Chunking { get; set; } = new();
}

public class ChunkingSettings
{
    public int MaxTokens { get; set; } = 256;
    public int OverlapTokens { get; set; } = 30;
}
