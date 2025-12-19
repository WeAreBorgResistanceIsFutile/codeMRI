namespace codeMRI.Infrastructure.Configuration;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string DocumentationModel { get; set; } = "llama3";
    public string ChatModel { get; set; } = "llama3";
    public int ContextSize { get; set; } = 4096;
    public double Temperature { get; set; } = 0.2;
    public List<string> JudgeModels { get; set; } = new();
    public ModelRoutingSettings ModelRouting { get; set; } = new();
}