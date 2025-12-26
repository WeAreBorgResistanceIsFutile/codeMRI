using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace codeMRI.Infrastructure.Services;

/// <summary>
///     Service for persisting debug snapshots of failed LLM requests
/// </summary>
public class DebugSnapshotService : IDebugSnapshotService
{
    private readonly ILogger<DebugSnapshotService> _logger;
    private const string DebugDirName = ".codemri/debug";

    public DebugSnapshotService(ILogger<DebugSnapshotService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveSnapshotAsync(string repoPath, DebugSnapshot snapshot)
    {
        try
        {
            var debugDir = Path.Combine(repoPath, DebugDirName);
            if (!Directory.Exists(debugDir))
            {
                Directory.CreateDirectory(debugDir);
            }

            var timestamp = snapshot.Timestamp.ToString("yyyyMMdd_HHmmss");
            var fileName = $"snapshot_{timestamp}_{snapshot.ComponentId.Replace("/", "_").Replace("\\", "_")}.json";
            var filePath = Path.Combine(debugDir, fileName);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(snapshot, options);

            await File.WriteAllTextAsync(filePath, json);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save debug snapshot for repo: {RepoPath}", repoPath);
            throw;
        }
    }

    public async Task<IEnumerable<DebugSnapshot>> GetSnapshotsAsync(string repoPath)
    {
        var snapshots = new List<DebugSnapshot>();
        var debugDir = Path.Combine(repoPath, DebugDirName);

        if (!Directory.Exists(debugDir))
        {
            return snapshots;
        }

        var files = Directory.GetFiles(debugDir, "snapshot_*.json");
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var snapshot = JsonSerializer.Deserialize<DebugSnapshot>(json);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load snapshot file: {File}", file);
            }
        }

        return snapshots.OrderByDescending(s => s.Timestamp);
    }
}
