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
    public List<ASTGraphNode> Nodes { get; set; } = new();
    public List<ASTGraphEdge> Edges { get; set; } = new();
}

public class ASTGraphNode
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public ASTNodeProperties Properties { get; set; } = new();
}

public class ASTNodeProperties
{
    public List<string> Annotations { get; set; } = new();
    public List<string> Decorators { get; set; } = new();
}

public class ASTGraphEdge
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Subtype { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
}