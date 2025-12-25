using codeMRI.MCP.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace codeMRI.MCP.UpdateStrategies;

public class PollingChangeDetector : BackgroundService
{
    private readonly GraphIndexService _indexService;
    private readonly IndexStateService _indexState;
    private readonly ILogger<PollingChangeDetector> _logger;
    private readonly string _repositoryPath;
    private readonly TimeSpan _pollInterval;
    
    private Dictionary<string, DateTime> _lastModified = new();
    private readonly object _lock = new();

    public PollingChangeDetector(
        GraphIndexService indexService,
        IndexStateService indexState,
        ILogger<PollingChangeDetector> logger,
        string repositoryPath,
        int pollIntervalSeconds = 10)
    {
        _indexService = indexService;
        _indexState = indexState;
        _logger = logger;
        _repositoryPath = repositoryPath;
        _pollInterval = TimeSpan.FromSeconds(pollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Polling change detector started. Interval: {Interval}s", _pollInterval.TotalSeconds);

        // Initial scan to populate lastModified
        await InitialScanAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
                await CheckForChangesAsync();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Polling change detector stopped");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling change detector");
            }
        }
    }

    private async Task InitialScanAsync()
    {
        _logger.LogDebug("Performing initial file scan");
        
        var files = Directory.EnumerateFiles(_repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(IsCodeFile)
            .ToList();

        lock (_lock)
        {
            foreach (var file in files)
            {
                try
                {
                    _lastModified[file] = File.GetLastWriteTimeUtc(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get last write time for: {File}", file);
                }
            }
        }

        _logger.LogInformation("Initial scan complete. Tracking {FileCount} files", _lastModified.Count);
    }

    private async Task CheckForChangesAsync()
    {
        var changedFiles = new List<string>();
        var newFiles = new List<string>();
        var deletedFiles = new List<string>();

        // Check for changes and new files
        var currentFiles = Directory.EnumerateFiles(_repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(IsCodeFile)
            .ToHashSet();

        lock (_lock)
        {
            // Check existing files for modifications
            foreach (var file in currentFiles)
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);

                    if (!_lastModified.TryGetValue(file, out var cachedTime))
                    {
                        // New file
                        newFiles.Add(file);
                        _lastModified[file] = lastWrite;
                    }
                    else if (lastWrite > cachedTime)
                    {
                        // Modified file
                        changedFiles.Add(file);
                        _lastModified[file] = lastWrite;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check file: {File}", file);
                }
            }

            // Check for deleted files
            var trackedFiles = _lastModified.Keys.ToList();
            foreach (var file in trackedFiles)
            {
                if (!currentFiles.Contains(file))
                {
                    deletedFiles.Add(file);
                    _lastModified.Remove(file);
                }
            }
        }

        // Process changes
        var allChanges = changedFiles.Concat(newFiles).ToList();
        
        if (allChanges.Any() || deletedFiles.Any())
        {
            _logger.LogInformation(
                "Detected changes: {Modified} modified, {New} new, {Deleted} deleted",
                changedFiles.Count, newFiles.Count, deletedFiles.Count);

            if (allChanges.Any())
            {
                _indexState.MarkFilesAsPending(allChanges);
                await _indexService.UpdateFilesAsync(allChanges);
            }

            // Note: Deleted files are already removed from the index by UpdateFilesAsync
            // when it detects the file no longer exists
        }
    }

    private static bool IsCodeFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".cs" or ".js" or ".ts" or ".py" or ".go" or ".rs" or ".java" or ".cpp" or ".c" or ".h";
    }
}
