using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IEvaluationMetricsSystem
{
    Task<DocumentationQualityMetrics> EvaluateDocumentationQualityAsync(WikiStructure structure, List<WikiPage> pages,
        List<CodeComponent> components);

    Task<CoverageMetrics> CalculateCoverageMetricsAsync(List<WikiPage> pages, List<CodeComponent> components);
    Task<ReadabilityMetrics> AnalyzeReadabilityAsync(List<WikiPage> pages);
    Task<BenchmarkReport> GenerateBenchmarkReportAsync(string repositoryPath, DocumentationQualityMetrics metrics);

    // Multi-judge consensus evaluation methods
    Task<ConsensusQualityScore> EvaluateWithMultipleJudgesAsync(WikiPage page, EvaluationRubric rubric,
        List<IJudgeAgent> judges);

    Task<QualityScore> EvaluateWithJudgesAsync(WikiPage page, EvaluationRubric rubric);
}

public class DocumentationQualityMetrics
{
    public double OverallScore { get; set; } // 0-100
    public CoverageMetrics Coverage { get; set; } = new();
    public ReadabilityMetrics Readability { get; set; } = new();
    public CompletenessMetrics Completeness { get; set; } = new();
    public AccuracyMetrics Accuracy { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class CoverageMetrics
{
    public double ComponentCoverage { get; set; } // Percentage of components documented
    public double ApiCoverage { get; set; } // Percentage of APIs documented
    public int TotalComponents { get; set; }
    public int DocumentedComponents { get; set; }
    public List<string> UndocumentedComponents { get; set; } = new();
}

public class ReadabilityMetrics
{
    public double AverageReadabilityScore { get; set; } // 0-100
    public double AverageWordCount { get; set; }
    public double AverageSectionCount { get; set; }
    public List<PageReadabilityScore> PageScores { get; set; } = new();
}

public class PageReadabilityScore
{
    public string PageId { get; set; } = string.Empty;
    public string PageTitle { get; set; } = string.Empty;
    public double Score { get; set; }
    public int WordCount { get; set; }
    public int SectionCount { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class CompletenessMetrics
{
    public double DescriptionCompleteness { get; set; } // 0-100
    public double ParameterDocumentation { get; set; } // 0-100
    public double ExampleCoverage { get; set; } // 0-100
    public int PagesWithExamples { get; set; }
    public int TotalPages { get; set; }
}

public class AccuracyMetrics
{
    public double ConsistencyScore { get; set; } // 0-100
    public double CurrencyScore { get; set; } // 0-100
    public List<string> Inconsistencies { get; set; } = new();
    public List<string> OutdatedReferences { get; set; } = new();
}

public class BenchmarkReport
{
    public string RepositoryPath { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DocumentationQualityMetrics Metrics { get; set; } = new();
    public List<BenchmarkComparison> Comparisons { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public List<string> ActionItems { get; set; } = new();
}

public class BenchmarkComparison
{
    public string Metric { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double IndustryAverage { get; set; }
    public double Percentile { get; set; }
    public string Status { get; set; } = string.Empty; // Excellent, Good, Average, Poor
}

public class ConsensusQualityScore : QualityScore
{
    public int JudgeCount { get; set; }
    public Dictionary<string, double> JudgeReliabilities { get; set; } = new();
    public double ConsensusScore { get; set; }
    public List<IndividualJudgeScore> IndividualScores { get; set; } = new();
    public bool MeetsMinimumJudgeRequirement { get; set; }
    public string ConsensusStatus { get; set; } = string.Empty;

    /// <summary>
    ///     Combined uncertainty from all judges, reflecting inter-judge disagreement.
    /// </summary>
    public double OverallUncertainty { get; set; }
}

public class IndividualJudgeScore
{
    public string JudgeId { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public Dictionary<string, RequirementScore> Breakdown { get; set; } = new();
    public double Reliability { get; set; }
    public Dictionary<string, double> StandardDeviation { get; set; } = new();

    /// <summary>
    ///     Uncertainty for this judge's overall score.
    /// </summary>
    public double OverallUncertainty { get; set; }
}