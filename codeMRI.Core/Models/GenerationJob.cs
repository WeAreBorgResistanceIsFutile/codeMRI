using System;
using codeMRI.Core.Models;

namespace codeMRI.Core.Models;

public enum GenerationStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public class GenerationJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RepoPath { get; set; } = string.Empty;
    public GenerationStatus Status { get; set; } = GenerationStatus.Queued;
    public int ProgressPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
    public string? ResultJson { get; set; } // Storing result as JSON blob for simplicity in SQLite
}
