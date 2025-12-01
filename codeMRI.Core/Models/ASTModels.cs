namespace codeMRI.Core.Models;

public class RawDependencyData
{
    public string Language { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public object? Tree { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public object Metrics { get; set; } = new();
    public DependencyGraphData DependencyGraph { get; set; } = new();
    public List<object> EntryPoints { get; set; } = new();
    public object HierarchicalStructure { get; set; } = new();
    public List<object> CrossModuleReferences { get; set; } = new();
}

public class DependencyGraphData
{
    public List<string> Dependencies { get; set; } = new();
}
