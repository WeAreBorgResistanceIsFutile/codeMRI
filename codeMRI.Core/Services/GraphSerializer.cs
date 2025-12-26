using codeMRI.Core.Models;
using System.Text.Json;

namespace codeMRI.Core.Services;

/// <summary>
///     Helper for serializing/deserializing EnhancedDependencyGraph using GraphIndexData DTO
/// </summary>
public static class GraphSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public static string Serialize(EnhancedDependencyGraph graph)
    {
        var data = new GraphIndexData
        {
            Nodes = graph.GetNodes().Select(MapToASTNode).ToList(),
            OutgoingEdges = graph.GetEdges()
                .GroupBy(e => e.From)
                .ToDictionary(g => g.Key, g => g.Select(MapToASTEdge).ToList()),
            IncomingEdges = graph.GetEdges()
                .GroupBy(e => e.To)
                .ToDictionary(g => g.Key, g => g.Select(MapToASTEdge).ToList())
        };

        return JsonSerializer.Serialize(data, _jsonOptions);
    }

    public static void DeserializeInto(string json, EnhancedDependencyGraph graph)
    {
        var data = JsonSerializer.Deserialize<GraphIndexData>(json, _jsonOptions);
        if (data == null) return;

        foreach (var node in data.Nodes)
        {
            var metadata = new NodeMetadata
            {
                Id = node.Id,
                Type = node.Type,
                Language = node.Language,
                FilePath = node.FilePath,
                Properties = new Dictionary<string, object>()
            };

            if (node.Properties != null)
            {
                if (node.Properties.ExtensionData != null)
                {
                    foreach (var kvp in node.Properties.ExtensionData)
                    {
                        metadata.Properties[kvp.Key] = kvp.Value;
                    }
                }
                
                metadata.Properties["Annotations"] = node.Properties.Annotations;
                metadata.Properties["Decorators"] = node.Properties.Decorators;
            }

            graph.AddNode(node.Id, metadata);
        }

        if (data.OutgoingEdges != null)
        {
            foreach (var edges in data.OutgoingEdges.Values)
            {
                foreach (var edge in edges)
                {
                    if (Enum.TryParse<EdgeType>(edge.Type, true, out var type))
                        graph.AddEdge(edge.Source, edge.Target, type, 1.0);
                    else
                        graph.AddEdge(edge.Source, edge.Target, EdgeType.Dependency, 1.0);
                }
            }
        }
    }

    private static ASTGraphNode MapToASTNode(GraphNode node)
    {
        var properties = new ASTNodeProperties
        {
            Annotations = node.Metadata.Properties.TryGetValue("Annotations", out var ann) ? (List<string>)ann : new List<string>(),
            Decorators = node.Metadata.Properties.TryGetValue("Decorators", out var dec) ? (List<string>)dec : new List<string>(),
            ExtensionData = new Dictionary<string, object>()
        };

        foreach (var kvp in node.Metadata.Properties)
        {
            if (kvp.Key == "Annotations" || kvp.Key == "Decorators") continue;
            properties.ExtensionData[kvp.Key] = kvp.Value;
        }

        return new ASTGraphNode
        {
            Id = node.ComponentId,
            Type = node.Metadata.Type,
            Language = node.Metadata.Language,
            FilePath = node.Metadata.FilePath,
            Properties = properties
        };
    }

    private static ASTGraphEdge MapToASTEdge(GraphEdge edge)
    {
        return new ASTGraphEdge
        {
            Source = edge.From,
            Target = edge.To,
            Type = edge.Type.ToString()
        };
    }
}
