using codeMRI.MCP.Services;
using codeMRI.MCP.Protocol;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace codeMRI.MCP.Tools;

public class TypeHierarchyTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;
    private readonly ILogger<TypeHierarchyTool> _logger;

    public TypeHierarchyTool(
        QueryEngine queryEngine,
        IndexStateService indexState,
        ILogger<TypeHierarchyTool> logger)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
        _logger = logger;
    }

    [McpTool("type_hierarchy")]
    [Description("Get the type hierarchy showing inheritance relationships (ancestors and descendants)")]
    public async Task<string> GetTypeHierarchyAsync(
        [Description("The type/class name to analyze")] string typeName,
        [Description("Direction: 'ancestors' (base classes), 'descendants' (derived classes), or 'both' (default: both)")] string direction = "both",
        [Description("Maximum depth to traverse (default: 10)")] int depth = 10)
    {
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}\n" +
                   $"⏱️ Estimated wait time: {gateResult.EstimatedWaitSeconds} seconds";
        }

        try
        {
            var hierarchy = await _queryEngine.GetTypeHierarchyAsync(typeName, depth, direction);

            var result = $"🏛️ Type Hierarchy for '{typeName}':\n\n";

            if (direction is "ancestors" or "both" && hierarchy.Ancestors.Any())
            {
                result += "⬆️ ANCESTORS (base classes/interfaces):\n";
                result += FormatTypeTree(hierarchy.Ancestors, 1);
                result += "\n";
            }

            if (direction is "descendants" or "both" && hierarchy.Descendants.Any())
            {
                result += "⬇️ DESCENDANTS (derived classes):\n";
                result += FormatTypeTree(hierarchy.Descendants, 1);
            }

            if (!hierarchy.Ancestors.Any() && !hierarchy.Descendants.Any())
            {
                result += "No inheritance relationships found.";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting type hierarchy for: {TypeName}", typeName);
            return $"Error: {ex.Message}";
        }
    }

    private string FormatTypeTree(List<TypeNode> nodes, int indent)
    {
        var result = "";
        var prefix = new string(' ', indent * 2);

        foreach (var node in nodes)
        {
            result += $"{prefix}├─ {node.NodeId} ({node.RelationType})\n";
            if (node.Children.Any())
            {
                result += FormatTypeTree(node.Children, indent + 1);
            }
        }

        return result;
    }
}
