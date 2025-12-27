using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Repository for persisting and retrieving benchmark data.
/// </summary>
public interface IBenchmarkRepository
{
    /// <summary>
    ///     Creates a new benchmark run.
    /// </summary>
    Task<BenchmarkRun> CreateAsync(BenchmarkRun run, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a benchmark run by ID.
    /// </summary>
    Task<BenchmarkRun?> GetByIdAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all benchmark runs.
    /// </summary>
    Task<List<BenchmarkRun>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets benchmark runs for a specific repository.
    /// </summary>
    Task<List<BenchmarkRun>> GetByRepositoryAsync(string repositoryUrl, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the currently active (running) benchmark run, if any.
    /// </summary>
    Task<BenchmarkRun?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing benchmark run.
    /// </summary>
    Task UpdateAsync(BenchmarkRun run, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a benchmark run and all associated data.
    /// </summary>
    Task DeleteAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a page benchmark to a run.
    /// </summary>
    Task AddPageBenchmarkAsync(string runId, PageBenchmark pageBenchmark, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all page benchmarks for a run.
    /// </summary>
    Task<List<PageBenchmark>> GetPageBenchmarksAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds metrics to a run (stored in batches for efficiency).
    /// </summary>
    Task AddMetricsAsync(string runId, BenchmarkMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all metrics for a run.
    /// </summary>
    Task<List<BenchmarkMetrics>> GetMetricsAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves a benchmark comparison.
    /// </summary>
    Task<ModelBenchmarkComparison> SaveComparisonAsync(ModelBenchmarkComparison comparison, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a benchmark comparison by ID.
    /// </summary>
    Task<ModelBenchmarkComparison?> GetComparisonAsync(string comparisonId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all benchmark comparisons.
    /// </summary>
    Task<List<ModelBenchmarkComparison>> GetAllComparisonsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a benchmark comparison.
    /// </summary>
    Task DeleteComparisonAsync(string comparisonId, CancellationToken cancellationToken = default);
}
