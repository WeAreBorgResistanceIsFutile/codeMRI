namespace codeMRI.Infrastructure.Configuration;

public class VectorStoreSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334; // gRPC default
    public string? ApiKey { get; set; }
    public bool UseHttps { get; set; } = false;
    
    public Dictionary<string, string> Collections { get; set; } = new()
    {
        { "Documentation", "documentation" },
        { "Code", "code" }
    };
}
