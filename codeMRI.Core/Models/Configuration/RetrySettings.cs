
namespace codeMRI.Core.Models.Configuration;

/// <summary>
/// Configuration for LLM Client Retry Mechanism
/// </summary>
public class RetrySettings
{
    /// <summary>
    /// Maximum number of retries for transient failures.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff.
    /// Default: 1000ms
    /// </summary>
    public int BaseDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds for any single retry wait.
    /// Default: 10000ms
    /// </summary>
    public int MaxDelayMilliseconds { get; set; } = 10000;
}
