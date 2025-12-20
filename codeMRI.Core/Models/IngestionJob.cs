namespace codeMRI.Core.Models;

public enum IngestionStatus
{
    Queued,
    Cloning,
    Analyzing,
    Generating,
    Completed,
    Failed,
    Cancelling,
    Cancelled
}

public class IngestionJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RepoUrl { get; set; } = string.Empty;
    public string RepoPath { get; set; } = string.Empty; // Local path
    public string RepoName { get; set; } = string.Empty;
    public IngestionStatus Status { get; set; } = IngestionStatus.Queued;
    public int ProgressPercentage { get; set; }
    public string CurrentPhase { get; set; } = "Initialized";
    public string Message { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty; // To track which instance is handling it
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
    public AudienceType Audience { get; set; } = AudienceType.Developer;
}