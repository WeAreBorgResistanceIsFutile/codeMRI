using codeMRI.MCP.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.MCP.Services;

public class IndexStateService
{
    private readonly ILogger<IndexStateService> _logger;
    private IndexState _state = IndexState.Initializing;
    private readonly HashSet<string> _indexedFiles = new();
    private readonly HashSet<string> _pendingFiles = new();
    private DateTime _lastIndexTime = DateTime.MinValue;
    private readonly object _lock = new();

    public IndexStateService(ILogger<IndexStateService> logger)
    {
        _logger = logger;
    }

    public IndexState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public async Task<QueryGateResult> CheckQueryGateAsync()
    {
        await Task.CompletedTask; // For async consistency
        
        lock (_lock)
        {
            if (_state == IndexState.Ready && _pendingFiles.Count == 0)
            {
                return QueryGateResult.Allow();
            }

            if (_state == IndexState.Indexing)
            {
                var total = _indexedFiles.Count + _pendingFiles.Count;
                var progress = total > 0 ? (_indexedFiles.Count * 100) / total : 0;
                var estimatedSeconds = (int)(_pendingFiles.Count * 0.1); // ~100ms per file
                
                return QueryGateResult.Block(
                    $"Indexing in progress ({progress}%). Please wait...",
                    estimatedSeconds
                );
            }

            if (_state == IndexState.Stale)
            {
                var estimatedSeconds = (int)(_pendingFiles.Count * 0.1);
                return QueryGateResult.Block(
                    $"{_pendingFiles.Count} files pending indexing. Refreshing...",
                    estimatedSeconds
                );
            }

            return QueryGateResult.Block("Index not ready", 5);
        }
    }

    public void MarkIndexingStarted(int totalFiles)
    {
        lock (_lock)
        {
            _state = IndexState.Indexing;
            _pendingFiles.Clear();
            _indexedFiles.Clear();
            _logger.LogInformation("Indexing started for {TotalFiles} files", totalFiles);
        }
    }

    public void MarkFileIndexed(string filePath)
    {
        lock (_lock)
        {
            _indexedFiles.Add(filePath);
            _pendingFiles.Remove(filePath);
        }
    }

    public void MarkIndexingComplete()
    {
        lock (_lock)
        {
            _state = IndexState.Ready;
            _lastIndexTime = DateTime.UtcNow;
            _pendingFiles.Clear();
            _logger.LogInformation("Indexing completed. {FileCount} files indexed", _indexedFiles.Count);
        }
    }

    public void MarkFilesAsPending(IEnumerable<string> files)
    {
        lock (_lock)
        {
            foreach (var file in files)
            {
                _pendingFiles.Add(file);
            }
            
            if (_state == IndexState.Ready && _pendingFiles.Count > 0)
            {
                _state = IndexState.Stale;
                _logger.LogWarning("Index marked as stale. {PendingCount} files need indexing", _pendingFiles.Count);
            }
        }
    }

    public List<string> GetUnindexedFiles()
    {
        lock (_lock)
        {
            return _pendingFiles.ToList();
        }
    }

    public (int indexed, int pending) GetStatistics()
    {
        lock (_lock)
        {
            return (_indexedFiles.Count, _pendingFiles.Count);
        }
    }
}
