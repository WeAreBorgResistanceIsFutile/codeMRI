using System.Collections.Concurrent;
using System.Text;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

/// <summary>
///     Service for running and managing LLM model benchmarks during ingestion.
/// </summary>
public class BenchmarkingService : IBenchmarkingService
{
    private readonly IBenchmarkRepository _repository;
    private readonly ILogger<BenchmarkingService> _logger;

    // In-memory buffer for metrics (flushed to repository periodically)
    private readonly ConcurrentDictionary<string, List<BenchmarkMetrics>> _metricsBuffer = new();

    public BenchmarkingService(IBenchmarkRepository repository, ILogger<BenchmarkingService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BenchmarkRun> StartBenchmarkRunAsync(
        string repositoryUrl,
        string name,
        string modelConfigJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting benchmark run '{Name}' for repository {Url}", name, repositoryUrl);

        var run = new BenchmarkRun
        {
            Name = name,
            RepositoryUrl = repositoryUrl,
            RepositoryName = ExtractRepositoryName(repositoryUrl),
            ModelConfiguration = modelConfigJson,
            StartTime = DateTime.UtcNow,
            Status = BenchmarkStatus.Running,
            CurrentPhase = "Starting"
        };

        return await _repository.CreateAsync(run, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BenchmarkRun> ResumeBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Benchmark run {runId} not found");

        if (run.Status != BenchmarkStatus.Paused)
        {
            throw new InvalidOperationException($"Cannot resume benchmark run with status {run.Status}");
        }

        _logger.LogInformation("Resuming benchmark run {RunId}", runId);

        run.Status = BenchmarkStatus.Running;
        await _repository.UpdateAsync(run, cancellationToken);

        return run;
    }

    /// <inheritdoc />
    public void RecordMetrics(string runId, BenchmarkMetrics metrics)
    {
        // Buffer metrics in memory and flush asynchronously
        _metricsBuffer.AddOrUpdate(
            runId,
            _ => new List<BenchmarkMetrics> { metrics },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(metrics);
                }
                return list;
            });

        // Fire-and-forget flush to repository
        _ = Task.Run(async () =>
        {
            try
            {
                await _repository.AddMetricsAsync(runId, metrics);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist metrics for run {RunId}", runId);
            }
        });
    }

    /// <inheritdoc />
    public async Task RecordPageBenchmarkAsync(string runId, PageBenchmark pageBenchmark, CancellationToken cancellationToken = default)
    {
        await _repository.AddPageBenchmarkAsync(runId, pageBenchmark, cancellationToken);

        _logger.LogDebug("Recorded page benchmark for {PageId} in run {RunId}",
            pageBenchmark.PageId, runId);
    }

    /// <inheritdoc />
    public async Task PauseBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Benchmark run {runId} not found");

        _logger.LogInformation("Pausing benchmark run {RunId}", runId);

        run.Status = BenchmarkStatus.Paused;
        await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BenchmarkRun> CompleteBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Benchmark run {runId} not found");

        var pageBenchmarks = await _repository.GetPageBenchmarksAsync(runId, cancellationToken);

        _logger.LogInformation("Completing benchmark run {RunId} with {PageCount} pages", runId, pageBenchmarks.Count);

        run.Status = BenchmarkStatus.Completed;
        run.EndTime = DateTime.UtcNow;
        run.TotalPages = pageBenchmarks.Count;
        run.SuccessfulPages = pageBenchmarks.Count(p => p.OverallQualityScore > 0);
        run.FailedPages = run.TotalPages - run.SuccessfulPages;

        // Calculate quality metrics
        if (pageBenchmarks.Count > 0)
        {
            var scores = pageBenchmarks.Select(p => p.OverallQualityScore).ToList();
            run.MeanQualityScore = scores.Average();
            run.MedianQualityScore = CalculateMedian(scores);
            run.QualityScoreStdDev = CalculateStdDev(scores, run.MeanQualityScore);
        }

        // Aggregate token counts from in-memory buffer
        if (_metricsBuffer.TryGetValue(runId, out var metrics))
        {
            lock (metrics)
            {
                run.TotalInputTokens = metrics.Sum(m => m.InputTokens);
                run.TotalOutputTokens = metrics.Sum(m => m.OutputTokens);
            }
        }

        await _repository.UpdateAsync(run, cancellationToken);

        return run;
    }

    /// <inheritdoc />
    public async Task CancelBenchmarkRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Benchmark run {runId} not found");

        _logger.LogInformation("Cancelling benchmark run {RunId}", runId);

        run.Status = BenchmarkStatus.Cancelled;
        run.EndTime = DateTime.UtcNow;
        await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ModelBenchmarkComparison> CompareRunsAsync(List<string> runIds, CancellationToken cancellationToken = default)
    {
        var runs = new List<BenchmarkRun>();
        foreach (var runId in runIds)
        {
            var run = await _repository.GetByIdAsync(runId, cancellationToken);
            if (run != null)
            {
                runs.Add(run);
            }
        }

        if (runs.Count < 2)
        {
            throw new InvalidOperationException("At least 2 benchmark runs are required for comparison");
        }

        _logger.LogInformation("Comparing {Count} benchmark runs", runs.Count);

        var comparison = new ModelBenchmarkComparison
        {
            RepositoryUrl = runs.First().RepositoryUrl,
            Runs = runs
        };

        // Determine fastest (shortest duration)
        var fastest = runs.OrderBy(r => r.TotalDuration).First();
        comparison.FastestConfiguration = fastest.Name;

        // Determine highest quality
        var highestQuality = runs.OrderByDescending(r => r.MeanQualityScore).First();
        comparison.HighestQualityConfiguration = highestQuality.Name;

        // Determine best value (quality per token)
        var bestValue = runs
            .Where(r => r.TotalTokens > 0)
            .OrderByDescending(r => r.MeanQualityScore / r.TotalTokens)
            .FirstOrDefault() ?? fastest;
        comparison.BestValueConfiguration = bestValue.Name;

        // Calculate rankings
        foreach (var run in runs)
        {
            comparison.QualityRankings[run.Name] = run.MeanQualityScore;
            comparison.SpeedRankings[run.Name] = 1.0 / (run.TotalDuration.TotalMinutes + 1);
            comparison.EfficiencyRankings[run.Name] = run.TotalTokens > 0
                ? run.MeanQualityScore / run.TotalTokens * 1000000
                : 0;
        }

        return await _repository.SaveComparisonAsync(comparison, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateReportAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Benchmark run {runId} not found");

        var pageBenchmarks = await _repository.GetPageBenchmarksAsync(runId, cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine($"# Benchmark Report: {run.RepositoryName}");
        sb.AppendLine();
        sb.AppendLine($"**Run Name:** {run.Name}");
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Status | {run.Status} |");
        sb.AppendLine($"| Duration | {run.TotalDuration:hh\\:mm\\:ss} |");
        sb.AppendLine($"| Total Pages | {run.TotalPages} |");
        sb.AppendLine($"| Mean Quality Score | {run.MeanQualityScore:F2} |");
        sb.AppendLine($"| Median Quality Score | {run.MedianQualityScore:F2} |");
        sb.AppendLine($"| Total Tokens | {run.TotalTokens:N0} |");
        sb.AppendLine();

        sb.AppendLine("## Model Configuration");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(run.ModelConfiguration);
        sb.AppendLine("```");
        sb.AppendLine();

        if (pageBenchmarks.Any())
        {
            sb.AppendLine("## Page Quality Scores");
            sb.AppendLine();
            sb.AppendLine("| Module | Quality Score | Word Count |");
            sb.AppendLine("|--------|--------------|------------|");
            foreach (var page in pageBenchmarks.OrderByDescending(p => p.OverallQualityScore).Take(20))
            {
                sb.AppendLine($"| {page.ModuleName} | {page.OverallQualityScore:F2} | {page.WordCount} |");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<BenchmarkRun?> GetActiveRunAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdatePhaseAsync(string runId, string phase, int progressPercentage, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Benchmark run {runId} not found");

        run.CurrentPhase = phase;
        run.ProgressPercentage = progressPercentage;

        await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BenchmarkMetrics?> GetMetricsForModuleAsync(string runId, string moduleId, CancellationToken cancellationToken = default)
    {
        if (_metricsBuffer.TryGetValue(runId, out var metrics))
        {
            lock (metrics)
            {
                // Find most recent for this module
                return metrics.LastOrDefault(m => m.ModuleId == moduleId);
            }
        }

        return null;
    }

    #region Helper Methods

    private static string ExtractRepositoryName(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "unknown";

        // Try to parse as URI, fall back to simple string handling
        if (Uri.TryCreate(url.TrimEnd('/'), UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[^1].Replace(".git", "") : "unknown";
        }

        // Not a valid URI - return the input or last segment
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1].Replace(".git", "") : url;
    }

    private static double CalculateMedian(List<double> values)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    private static double CalculateStdDev(List<double> values, double mean)
    {
        if (values.Count < 2) return 0;

        var sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquares / values.Count);
    }

    #endregion
}
