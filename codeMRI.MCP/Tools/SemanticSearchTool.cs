using codeMRI.MCP.Services;
using codeMRI.MCP.Protocol;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace codeMRI.MCP.Tools;

public class SemanticSearchTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;
    private readonly ILogger<SemanticSearchTool> _logger;

    public SemanticSearchTool(
        QueryEngine queryEngine,
        IndexStateService indexState,
        ILogger<SemanticSearchTool> logger)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
        _logger = logger;
    }

    [McpTool("semantic_search")]
    [Description("Search for code elements by name, description, or purpose")]
    public async Task<string> SearchAsync(
        [Description("Search query (symbol name or description)")] string query,
        [Description("Type filter: 'class', 'method', 'interface', or 'any' (default: any)")] string type = "any",
        [Description("Maximum number of results (default: 10)")] int limit = 10)
    {
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}\n" +
                   $"⏱️ Estimated wait time: {gateResult.EstimatedWaitSeconds} seconds";
        }

        try
        {
            var results = await _queryEngine.SemanticSearchAsync(query, type, limit);

            if (results.Count == 0)
            {
                return $"No results found for query: {query}";
            }

            var result = $"🔎 Found {results.Count} result(s) for '{query}'";
            if (!type.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                result += $" (type: {type})";
            }
            result += ":\n\n";

            foreach (var node in results)
            {
                result += $"  • {node.Id}\n";
                result += $"    Type: {node.Type}\n";
                result += $"    Language: {node.Language}\n";
                
                if (node.Properties?.Annotations?.Any() == true)
                {
                    result += $"    Annotations: {string.Join(", ", node.Properties.Annotations)}\n";
                }
                
                if (node.Properties?.Decorators?.Any() == true)
                {
                    result += $"    Decorators: {string.Join(", ", node.Properties.Decorators)}\n";
                }
                
                result += "\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search for: {Query}", query);
            return $"Error: {ex.Message}";
        }
    }
}
