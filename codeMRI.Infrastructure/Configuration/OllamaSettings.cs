namespace codeMRI.Infrastructure.Configuration;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public int ContextSize { get; set; } = 4096;
    public double Temperature { get; set; } = 0.2;
}