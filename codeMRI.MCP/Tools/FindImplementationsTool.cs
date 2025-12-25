using codeMRI.MCP.Services;
using codeMRI.MCP.Protocol;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace codeMRI.MCP.Tools;

public class FindImplementationsTool
{
    private readonly QueryEngine _queryEngine;
    private readonly IndexStateService _indexState;
    private readonly ILogger<FindImplementationsTool> _logger;

    public FindImplementationsTool(
        QueryEngine queryEngine,
        IndexStateService indexState,
        ILogger<FindImplementationsTool> logger)
    {
        _queryEngine = queryEngine;
        _indexState = indexState;
        _logger = logger;
    }

    [McpTool("find_implementations")]
    [Description("Find all implementations of an interface or abstract class")]
    public async Task<string> FindImplementationsAsync(
        [Description("The interface or base class name")] string interfaceOrBaseClass)
    {
        var gateResult = await _indexState.CheckQueryGateAsync();
        if (!gateResult.Allowed)
        {
            return $"❌ Query blocked: {gateResult.BlockReason}\n" +
                   $"⏱️ Estimated wait time: {gateResult.EstimatedWaitSeconds} seconds";
        }

        try
        {
            var implementations = await _queryEngine.FindImplementationsAsync(interfaceOrBaseClass);

            if (implementations.Count == 0)
            {
                return $"No implementations found for: {interfaceOrBaseClass}";
            }

            var result = $"🔍 Found {implementations.Count} implementation(s) of '{interfaceOrBaseClass}':\n\n";
            
            foreach (var impl in implementations)
            {
                result += $"  • {impl.Id}\n";
                result += $"    Type: {impl.Type}\n";
                result += $"    Language: {impl.Language}\n";
                
                if (impl.Properties?.Annotations?.Any() == true)
                {
                    result += $"    Annotations: {string.Join(", ", impl.Properties.Annotations)}\n";
                }
                
                result += "\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding implementations for: {Interface}", interfaceOrBaseClass);
            return $"Error: {ex.Message}";
        }
    }
}
