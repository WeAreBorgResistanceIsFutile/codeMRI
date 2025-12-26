using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for persisting debug snapshots of failed LLM requests
/// </summary>
public interface IDebugSnapshotService
{
    /// <summary>
    ///     Saves a debug snapshot to disk
    /// </summary>
    /// <param name="repoPath">Path to the repository</param>
    /// <param name="snapshot">The snapshot to save</param>
    Task<string> SaveSnapshotAsync(string repoPath, DebugSnapshot snapshot);

    /// <summary>
    ///     Gets all snapshots for a given repository
    /// </summary>
    Task<IEnumerable<DebugSnapshot>> GetSnapshotsAsync(string repoPath);
}
