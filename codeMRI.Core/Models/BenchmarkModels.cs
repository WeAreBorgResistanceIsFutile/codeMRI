namespace codeMRI.Core.Models;

/// <summary>
///     Metrics collected for a single LLM invocation.
/// </summary>
public class BenchmarkMetrics
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ModelName { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;  // e.g., "LeafPageGeneration", "ParentSynthesis", "Judge"
    public string Phase { get; set; } = string.Empty;      // e.g., "ContentGeneration", "Evaluation"
    
    // Timing metrics
    public TimeSpan Latency { get; set; }
    public TimeSpan Duration { get => Latency; set => Latency = value; } // Alias for compatibility
    public DateTime StartTime { get; set; }
    public DateTime Timestamp { get => StartTime; set => StartTime = value; } // Alias for compatibility
    public DateTime EndTime { get; set; }
    
    // Token metrics
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
    public double TokensPerSecond => Latency.TotalSeconds > 0 ? OutputTokens / Latency.TotalSeconds : 0;
    
    // Context
    public int ContextWindowSize { get; set; } // Added missing property
    public string ModuleId { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    
    // Error tracking
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
///     Quality assessment for a single generated page.
/// </summary>
public class PageBenchmark
{
    public string PageId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    
    // Generation metrics
    public BenchmarkMetrics GenerationMetrics { get; set; } = new();
    
    // Quality metrics (from EvaluationMetricsSystem)
    public double OverallQualityScore { get; set; }
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public double JudgeConsensusLevel { get; set; }
    public List<RequirementAssessment> RequirementAssessments { get; set; } = new();
    
    // Content metrics
    public int WordCount { get; set; }
    public int SectionCount { get; set; }
    public int CodeBlockCount { get; set; }
    public int DiagramCount { get; set; }
}

/// <summary>
///     Status of a benchmark run.
/// </summary>
public enum BenchmarkStatus
{
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
///     Complete benchmark run for a single model configuration.
///     Supports resumability via CurrentPhase and ProcessedModules tracking.
/// </summary>
public class BenchmarkRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ModelConfiguration { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan TotalDuration => (EndTime ?? DateTime.UtcNow) - StartTime;
    
    // Status for resumability
    public BenchmarkStatus Status { get; set; } = BenchmarkStatus.Running;
    public string CurrentPhase { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public HashSet<string> ProcessedModules { get; set; } = new();
    
    // Aggregate metrics
    public int TotalPages { get; set; }
    public int SuccessfulPages { get; set; }
    public int FailedPages { get; set; }
    
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalTokens => TotalInputTokens + TotalOutputTokens;
    
    public double MeanQualityScore { get; set; }
    public double MedianQualityScore { get; set; }
    public double QualityScoreStdDev { get; set; }
    
    // Per-phase timing
    public Dictionary<string, TimeSpan> PhaseDurations { get; set; } = new();
    
    // Per-task type metrics
    public Dictionary<string, AggregateMetrics> TaskTypeMetrics { get; set; } = new();
    
    // Individual page benchmarks
    public List<PageBenchmark> PageBenchmarks { get; set; } = new();
}

/// <summary>
///     Aggregated metrics for a task type or phase.
/// </summary>
public class AggregateMetrics
{
    public int Count { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan MeanTime { get; set; }
    public TimeSpan MedianTime { get; set; }
    public TimeSpan P95Time { get; set; }
    public long TotalTokens { get; set; }
    public double MeanTokensPerCall { get; set; }
    public double SuccessRate { get; set; }
    public int TotalRetries { get; set; }
}

/// <summary>
///     Comparison report between multiple model configurations.
/// </summary>
public class ModelBenchmarkComparison
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RepositoryUrl { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    public List<BenchmarkRun> Runs { get; set; } = new();
    
    // Comparison results
    public string FastestConfiguration { get; set; } = string.Empty;
    public string HighestQualityConfiguration { get; set; } = string.Empty;
    public string BestValueConfiguration { get; set; } = string.Empty;
    
    public Dictionary<string, double> QualityRankings { get; set; } = new();
    public Dictionary<string, double> SpeedRankings { get; set; } = new();
    public Dictionary<string, double> EfficiencyRankings { get; set; } = new();
}

public class StartBenchmarkRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelConfiguration { get; set; } = string.Empty;
}
