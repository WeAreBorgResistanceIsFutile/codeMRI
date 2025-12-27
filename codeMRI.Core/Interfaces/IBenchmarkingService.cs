using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for running and managing LLM model benchmarks during ingestion.
/// </summary>
public interface IBenchmarkingService
{
    /// <summary>
    ///     Starts a new benchmark run.
    /// </summary>
    /// <param name="repositoryUrl">The repository URL to benchmark.</param>
    /// <param name="name">Human-readable name for this benchmark run.</param>
    /// <param name="modelConfigJson">JSON serialized model routing configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BenchmarkRun> StartBenchmarkRunAsync(
        string repositoryUrl,
        string name,
        string modelConfigJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resumes a paused/interrupted benchmark run.
    /// </summary>
    Task<BenchmarkRun> ResumeBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records metrics for a single LLM invocation.
    /// </summary>
    void RecordMetrics(string runId, BenchmarkMetrics metrics);

    /// <summary>
    ///     Records completion of a page with its quality assessment.
    /// </summary>
    Task RecordPageBenchmarkAsync(string runId, PageBenchmark pageBenchmark, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pauses a running benchmark (for later resume).
    /// </summary>
    Task PauseBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes and finalizes a benchmark run.
    /// </summary>
    Task<BenchmarkRun> CompleteBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a running benchmark.
    /// </summary>
    Task CancelBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Compares multiple benchmark runs.
    /// </summary>
    Task<ModelBenchmarkComparison> CompareRunsAsync(List<string> runIds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates a Markdown report for a benchmark run.
    /// </summary>
    Task<string> GenerateReportAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the current active benchmark run (if any).
    /// </summary>
    Task<BenchmarkRun?> GetActiveRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the current phase of a benchmark run.
    /// </summary>
    Task UpdatePhaseAsync(string runId, string phase, int progressPercentage, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets recently recorded metrics for a specific module.
    /// </summary>
    Task<BenchmarkMetrics?> GetMetricsForModuleAsync(string runId, string moduleId, CancellationToken cancellationToken = default);
}
