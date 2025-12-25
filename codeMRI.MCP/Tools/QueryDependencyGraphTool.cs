using codeMRI.MCP.Services;
using codeMRI.MCP.Protocol;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace codeMRI.MCP.Tools;

public class QueryDependencyGraphTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;
    private readonly ILogger<QueryDependencyGraphTool> _logger;

    public QueryDependencyGraphTool(
        QueryEngine queryEngine,
        IndexStateService indexState,
        ILogger<QueryDependencyGraphTool> logger)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
        _logger = logger;
    }

    [McpTool("query_dependencies")]
    [Description("Query the dependency graph for a component to see what it depends on or what depends on it")]
    public async Task<string> QueryDependenciesAsync(
        [Description("The component/node ID to analyze")] string nodeId,
        [Description("Direction: 'dependencies' (what this depends on) or 'dependents' (what depends on this)")] string direction = "dependencies",
        [Description("Maximum depth to traverse (default: 3)")] int depth = 3)
    {
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}\n" +
                   $"⏱️ Estimated wait time: {gateResult.EstimatedWaitSeconds} seconds";
        }

        try
        {
            var tree = await _queryEngine.GetDependencyTreeAsync(nodeId, depth, direction);

            var result = $"📦 Dependency Tree for '{nodeId}':\n\n";

            if (direction.Equals("dependencies", StringComparison.OrdinalIgnoreCase))
            {
                result += "⬇️ DEPENDENCIES (what this component depends on):\n";
            }
            else
            {
                result += "⬆️ DEPENDENTS (what depends on this component):\n";
            }

            if (tree.Dependencies.Any())
            {
                result += FormatDependencyTree(tree.Dependencies, 1);
            }
            else
            {
                result += "  No dependencies found.\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying dependencies for: {NodeId}", nodeId);
            return $"Error: {ex.Message}";
        }
    }

    private string FormatDependencyTree(List<DependencyNode> nodes, int indent)
    {
        var result = "";
        var prefix = new string(' ', indent * 2);

        foreach (var node in nodes)
        {
            result += $"{prefix}├─ {node.NodeId}\n";
            if (node.Children.Any())
            {
                result += FormatDependencyTree(node.Children, indent + 1);
            }
        }

        return result;
    }
}
