using codeMRI.MCP.Services;
using codeMRI.MCP.Protocol;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace codeMRI.MCP.Tools;

public class RefreshGraphTool
{
    private readonly GraphIndexService _graphIndex;
    private readonly IndexStateService _indexState;
    private readonly ILogger<RefreshGraphTool> _logger;
    private readonly string _repositoryPath;

    public RefreshGraphTool(
        GraphIndexService graphIndex,
        IndexStateService indexState,
        ILogger<RefreshGraphTool> logger)
    {
        _graphIndex = graphIndex;
        _indexState = indexState;
        _logger = logger;
        
        // Get repository path from environment or current directory
        _repositoryPath = Environment.GetEnvironmentVariable("CODEMRI_REPO_PATH") 
                         ?? Directory.GetCurrentDirectory();
    }

    [McpTool("refresh_graph")]
    [Description("Manually trigger a refresh of the code graph index")]
    public async Task<string> RefreshAsync(
        [Description("Scope: 'all' (full re-index), 'file' (single file), or 'directory' (directory tree)")] string scope = "all",
        [Description("Path to file or directory (required if scope is 'file' or 'directory')")] string? path = null)
    {
        try
        {
            var (indexed, pending) = _indexState.GetStatistics();
            
            var result = $"🔄 Refresh Graph Index\n\n";
            result += $"Current state: {_indexState.CurrentState}\n";
            result += $"Indexed files: {indexed}\n";
            result += $"Pending files: {pending}\n\n";

            if (scope.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                result += "Starting full repository re-index...\n";
                _logger.LogInformation("Manual full re-index triggered");
                
                await _graphIndex.IndexRepositoryAsync(_repositoryPath);
                
                result += "✅ Full re-index completed successfully!";
            }
            else if (scope.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(path))
                {
                    return "❌ Error: 'path' parameter is required when scope is 'file'";
                }

                result += $"Refreshing file: {path}\n";
                _logger.LogInformation("Manual file refresh triggered for: {Path}", path);
                
                await _graphIndex.UpdateFilesAsync(new List<string> { path });
                
                result += "✅ File refreshed successfully!";
            }
            else if (scope.Equals("directory", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(path))
                {
                    return "❌ Error: 'path' parameter is required when scope is 'directory'";
                }

                var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(IsCodeFile)
                    .ToList();

                result += $"Refreshing {files.Count} files in directory: {path}\n";
                _logger.LogInformation("Manual directory refresh triggered for: {Path}", path);
                
                await _graphIndex.UpdateFilesAsync(files);
                
                result += $"✅ Directory refreshed successfully! ({files.Count} files)";
            }
            else
            {
                return $"❌ Error: Invalid scope '{scope}'. Must be 'all', 'file', or 'directory'";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing graph: {Scope}, {Path}", scope, path);
            return $"❌ Error: {ex.Message}";
        }
    }

    private static bool IsCodeFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".cs" or ".js" or ".ts" or ".py" or ".go" or ".rs" or ".java" or ".cpp" or ".c" or ".h";
    }
}
