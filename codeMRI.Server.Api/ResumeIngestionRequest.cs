namespace codeMRI.Server.Api;

/// <summary>
/// Request model for resuming ingestion
/// </summary>
public class ResumeIngestionRequest
{
    /// <summary>
    /// Path to the repository to ingest
    /// </summary>
    public string RepoPath { get; set; } = string.Empty;

    /// <summary>
    /// Batch size for processing (optional)
    /// </summary>
    public int? BatchSize { get; set; }
}
