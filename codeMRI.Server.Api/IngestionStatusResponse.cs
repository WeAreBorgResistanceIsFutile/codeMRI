namespace codeMRI.Server.Api;

/// <summary>
/// Response model for ingestion status
/// </summary>
public class IngestionStatusResponse
{
    /// <summary>
    /// Current status of the ingestion process
    /// </summary>
    public string Status { get; set; } = "idle";

    /// <summary>
    /// Total number of files to process
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Number of files processed so far
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    /// Number of files remaining to process
    /// </summary>
    public int RemainingFiles { get; set; }

    /// <summary>
    /// Number of files that failed to process
    /// </summary>
    public int FailedFiles { get; set; }

    /// <summary>
    /// Percentage of completion (0-100)
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Timestamp when processing started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when processing completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Timestamp of the last checkpoint
    /// </summary>
    public DateTime? LastCheckpoint { get; set; }

    /// <summary>
    /// Sample of recent errors (up to 5)
    /// </summary>
    public List<string> RecentErrors { get; set; } = new();

    /// <summary>
    /// Whether the ingestion can be resumed
    /// </summary>
    public bool CanResume { get; set; }
}
