namespace codeMRI.Core.Models;

public class CrossReference
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public EdgeType Type { get; set; }
    public int Index { get; set; }
    public int Length { get; set; }
    public string Context { get; set; } = string.Empty;
}

public class RegisteredComponent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DocPath { get; set; } = string.Empty;
    public HashSet<string> RelatedComponentIds { get; set; } = new();
}