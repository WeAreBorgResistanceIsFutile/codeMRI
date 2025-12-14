using System.Collections.Generic;
using System.Threading.Tasks;
using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IIngestionJobManager
{
    /// <summary>
    /// Starts a new ingestion job for the given Git URL.
    /// </summary>
    Task<IngestionJob> StartJobAsync(string repoUrl, bool forceRegenerate, string? connectionId = null);

    /// <summary>
    /// Gets the current status of a job.
    /// </summary>
    Task<IngestionJob?> GetJobAsync(string jobId);

    /// <summary>
    /// Lists all active or recent jobs.
    /// </summary>
    Task<List<IngestionJob>> ListActiveJobsAsync();

    /// <summary>
    /// Requests cancellation of a job.
    /// </summary>
    Task CancelJobAsync(string jobId);
}
