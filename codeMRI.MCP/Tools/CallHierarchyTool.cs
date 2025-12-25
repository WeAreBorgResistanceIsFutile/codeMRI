using codeMRI.MCP.Services;
using codeMRI.MCP.Protocol;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace codeMRI.MCP.Tools;

public class CallHierarchyTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;
    private readonly ILogger<CallHierarchyTool> _logger;

    public CallHierarchyTool(
        QueryEngine queryEngine,
        IndexStateService indexState,
        ILogger<CallHierarchyTool> logger)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
        _logger = logger;
    }

    [McpTool("call_hierarchy")]
    [Description("Get the call hierarchy showing who calls a method and what it calls")]
    public async Task<string> GetCallHierarchyAsync(
        [Description("The method name to analyze")] string method,
        [Description("Direction: 'callers', 'callees', or 'both' (default: both)")] string direction = "both",
        [Description("Maximum depth to traverse (default: 5)")] int depth = 5)
    {
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}\n" +
                   $"⏱️ Estimated wait time: {gateResult.EstimatedWaitSeconds} seconds";
        }

        try
        {
            var hierarchy = await _queryEngine.GetCallHierarchyAsync(method, depth, direction);

            var result = $"📞 Call Hierarchy for '{method}':\n\n";

            if (direction is "callers" or "both" && hierarchy.Callers.Any())
            {
                result += "👆 CALLERS (who calls this method):\n";
                result += FormatCallTree(hierarchy.Callers, 1);
                result += "\n";
            }

            if (direction is "callees" or "both" && hierarchy.Callees.Any())
            {
                result += "👇 CALLEES (what this method calls):\n";
                result += FormatCallTree(hierarchy.Callees, 1);
            }

            if (!hierarchy.Callers.Any() && !hierarchy.Callees.Any())
            {
                result += "No call relationships found.";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting call hierarchy for method: {Method}", method);
            return $"Error: {ex.Message}";
        }
    }

    private string FormatCallTree(List<CallNode> nodes, int indent)
    {
        var result = "";
        var prefix = new string(' ', indent * 2);

        foreach (var node in nodes)
        {
            result += $"{prefix}├─ {node.NodeId} ({node.EdgeType})\n";
            if (node.Children.Any())
            {
                result += FormatCallTree(node.Children, indent + 1);
            }
        }

        return result;
    }
}
