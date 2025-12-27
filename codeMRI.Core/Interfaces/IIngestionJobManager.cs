using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IIngestionJobManager
{
    /// <summary>
    ///     Starts a new ingestion job for the given Git URL.
    /// </summary>
    Task<IngestionJob> StartJobAsync(string repoUrl, bool forceRegenerate,
        AudienceType audience = AudienceType.Developer, string? connectionId = null);

    /// <summary>
    ///     Restarts or resumes an existing job.
    /// </summary>
    Task<IngestionJob> RestartJobAsync(string jobId, bool forceRegenerate, string? connectionId = null);

    /// <summary>
    ///     Gets the current status of a job.
    /// </summary>
    Task<IngestionJob?> GetJobAsync(string jobId);

    /// <summary>
    ///     Lists all active or recent jobs.
    /// </summary>
    Task<List<IngestionJob>> ListActiveJobsAsync();

    /// <summary>
    ///     Lists all jobs (active and completed).
    /// </summary>
    Task<List<IngestionJob>> ListAllJobsAsync();

    /// <summary>
    ///     Requests cancellation of a job.
    /// </summary>
    Task CancelJobAsync(string jobId);

    /// <summary>
    ///     Deletes a job from the registry.
    /// </summary>
    Task DeleteJobAsync(string jobId);
}