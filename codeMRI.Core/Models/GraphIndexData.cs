namespace codeMRI.Core.Models;

/// <summary>
///     DTO for serializing the enhanced dependency and hierarchy graph
/// </summary>
public class GraphIndexData
{
    public List<ASTGraphNode> Nodes { get; set; } = new();
    public Dictionary<string, List<ASTGraphEdge>> IncomingEdges { get; set; } = new();
    public Dictionary<string, List<ASTGraphEdge>> OutgoingEdges { get; set; } = new();
}
