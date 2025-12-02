namespace codeMRI.Core.Interfaces;

public interface IASTServiceClient
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default);

    Task<ASTParseResult?> ParseCodeAsync(string code, string language, string filePath = "",
        CancellationToken cancellationToken = default);

    Task<List<CodeComponent>> ConvertToCodeComponentsAsync(ASTParseResult astResult,
        CancellationToken cancellationToken = default);
}

public class ASTParseResult
{
    public string Language { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public object? Tree { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public object Metrics { get; set; } = new();
    public object DependencyGraph { get; set; } = new();
    public List<object> EntryPoints { get; set; } = new();
    public object HierarchicalStructure { get; set; } = new();
    public List<object> CrossModuleReferences { get; set; } = new();
}