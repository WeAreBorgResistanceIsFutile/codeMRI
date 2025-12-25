using codeMRI.MCP.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using codeMRI.MCP.Protocol;


namespace codeMRI.MCP.Tools;

public class FindReferencesTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;
    private readonly ILogger<FindReferencesTool> _logger;

    public FindReferencesTool(
        QueryEngine queryEngine,
        IndexStateService indexState,
        ILogger<FindReferencesTool> logger)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
        _logger = logger;
    }

    [McpTool("find_references")]
    [Description("Find all references to a symbol (class, method, variable) in the codebase")]
    public async Task<string> FindReferencesAsync(
        [Description("The symbol name to search for")] string symbol,
        [Description("Optional file path to filter results")] string? filePath = null)
    {
        // Check if index is ready
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}\n" +
                   $"⏱️ Estimated wait time: {gateResult.EstimatedWaitSeconds} seconds\n" +
                   $"Please try again in a moment.";
        }

        try
        {
            var references = await _queryEngine.FindAllReferencesAsync(symbol, filePath);

            if (references.Count == 0)
            {
                return $"No references found for symbol: {symbol}";
            }

            var result = $"Found {references.Count} references to '{symbol}':\n\n";
            foreach (var reference in references)
            {
                result += $"  • {reference.ReferencedFrom} ({reference.EdgeType})\n";
                if (!string.IsNullOrEmpty(reference.FilePath))
                {
                    result += $"    File: {reference.FilePath}\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding references for symbol: {Symbol}", symbol);
            return $"Error: {ex.Message}";
        }
    }
}
