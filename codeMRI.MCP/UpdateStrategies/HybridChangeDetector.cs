using codeMRI.MCP.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace codeMRI.MCP.UpdateStrategies;

/// <summary>
/// Hybrid change detector that tries FileSystemWatcher first, falls back to polling
/// </summary>
public class HybridChangeDetector : IHostedService
{
    private readonly GraphIndexService _indexService;
    private readonly IndexStateService _indexState;
    private readonly ILogger<HybridChangeDetector> _logger;
    private readonly string _repositoryPath;
    private readonly int _pollIntervalSeconds;
    
    private FileSystemWatcher? _watcher;
    private PollingChangeDetector? _poller;
    private Timer? _debounceTimer;
    private readonly HashSet<string> _pendingChanges = new();
    private readonly object _lock = new();

    public HybridChangeDetector(
        GraphIndexService indexService,
        IndexStateService indexState,
        ILogger<HybridChangeDetector> logger,
        string repositoryPath,
        int pollIntervalSeconds = 10)
    {
        _indexService = indexService;
        _indexState = indexState;
        _logger = logger;
        _repositoryPath = repositoryPath;
        _pollIntervalSeconds = pollIntervalSeconds;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Try FileSystemWatcher first
        if (TryStartFileWatcher())
        {
            _logger.LogInformation("Using FileSystemWatcher for change detection");
            return;
        }

        // Fallback to polling
        _logger.LogWarning("FileSystemWatcher not available, falling back to polling");
        
        // Create a logger factory with console logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        _poller = new PollingChangeDetector(
            _indexService,
            _indexState,
            loggerFactory.CreateLogger<PollingChangeDetector>(),
            _repositoryPath,
            _pollIntervalSeconds);
        
        await _poller.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            _logger.LogInformation("FileSystemWatcher stopped");
        }

        if (_poller != null)
        {
            await _poller.StopAsync(cancellationToken);
            _logger.LogInformation("Polling change detector stopped");
        }

        _debounceTimer?.Dispose();
    }

    private bool TryStartFileWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(_repositoryPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            _watcher.EnableRaisingEvents = true;

            // Test if events are actually working by creating a test file
            var testFile = Path.Combine(_repositoryPath, $".mcp_test_{Guid.NewGuid()}.tmp");
            try
            {
                File.WriteAllText(testFile, "test");
                Thread.Sleep(100); // Give watcher time to fire
                File.Delete(testFile);
            }
            catch
            {
                // Ignore test file errors
            }

            _logger.LogInformation("FileSystemWatcher initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileSystemWatcher initialization failed");
            _watcher?.Dispose();
            _watcher = null;
            return false;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsCodeFile(e.FullPath))
            return;

        lock (_lock)
        {
            _pendingChanges.Add(e.FullPath);
        }

        // Reset debounce timer (wait 500ms after last change)
        _debounceTimer?.Change(500, Timeout.Infinite);
        _debounceTimer ??= new Timer(OnDebounceElapsed, null, 500, Timeout.Infinite);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsCodeFile(e.FullPath))
            return;

        lock (_lock)
        {
            // Remove old path, add new path
            _pendingChanges.Remove(e.OldFullPath);
            _pendingChanges.Add(e.FullPath);
        }

        _debounceTimer?.Change(500, Timeout.Infinite);
        _debounceTimer ??= new Timer(OnDebounceElapsed, null, 500, Timeout.Infinite);
    }

    private async void OnDebounceElapsed(object? state)
    {
        List<string> filesToUpdate;
        
        lock (_lock)
        {
            filesToUpdate = new List<string>(_pendingChanges);
            _pendingChanges.Clear();
        }

        if (!filesToUpdate.Any())
            return;

        _logger.LogInformation("Processing {FileCount} file changes", filesToUpdate.Count);

        try
        {
            _indexState.MarkFilesAsPending(filesToUpdate);
            await _indexService.UpdateFilesAsync(filesToUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file changes");
        }
    }

    private static bool IsCodeFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".cs" or ".js" or ".ts" or ".py" or ".go" or ".rs" or ".java" or ".cpp" or ".c" or ".h";
    }
}
