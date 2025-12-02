namespace codeMRI.Infrastructure.Configuration;

public class QdrantSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334; // gRPC port
    public string ApiKey { get; set; } = string.Empty;
    public int VectorSize { get; set; } = 768; // Default for nomic-embed-text
}