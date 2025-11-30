using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Services;

public class EvaluationMetricsSystem : IEvaluationMetricsSystem
{
    private readonly ILogger<EvaluationMetricsSystem> _logger;
    private readonly Dictionary<string, double> _industryBenchmarks = new()
    {
        { "ComponentCoverage", 85.0 },
        { "ApiCoverage", 90.0 },
        { "ReadabilityScore", 75.0 },
        { "DescriptionCompleteness", 80.0 },
        { "ExampleCoverage", 60.0 }
    };

    public EvaluationMetricsSystem(ILogger<EvaluationMetricsSystem> logger)
    {
        _logger = logger;
    }

    public async Task<DocumentationQualityMetrics> EvaluateDocumentationQualityAsync(
        WikiStructure structure, 
        List<WikiPage> pages, 
        List<CodeComponent> components)
    {
        _logger.LogInformation("Starting documentation quality evaluation");

        var metrics = new DocumentationQualityMetrics();

        // Calculate coverage metrics
        metrics.Coverage = await CalculateCoverageMetricsAsync(pages, components);

        // Analyze readability
        metrics.Readability = await AnalyzeReadabilityAsync(pages);

        // Calculate completeness
        metrics.Completeness = CalculateCompletenessMetrics(pages, components);

        // Calculate accuracy
        metrics.Accuracy = CalculateAccuracyMetrics(pages, components);

        // Calculate overall score
        metrics.OverallScore = CalculateOverallScore(metrics);

        // Generate recommendations
        metrics.Recommendations = GenerateRecommendations(metrics);

        _logger.LogInformation("Documentation quality evaluation completed with overall score: {Score}", metrics.OverallScore);
        return metrics;
    }

    public Task<CoverageMetrics> CalculateCoverageMetricsAsync(List<WikiPage> pages, List<CodeComponent> components)
    {
        var documentedComponentIds = pages.Select(p => p.Id).ToHashSet();
        var undocumentedComponents = components
            .Where(c => !documentedComponentIds.Contains(c.Id))
            .Select(c => c.Name)
            .ToList();

        var apiComponents = components.Where(c => 
            c.Type.Equals("Controller", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("Service", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("API", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var documentedApis = apiComponents.Count(c => documentedComponentIds.Contains(c.Id));

        return Task.FromResult(new CoverageMetrics
        {
            TotalComponents = components.Count,
            DocumentedComponents = documentedComponentIds.Count,
            ComponentCoverage = components.Count > 0 ? (double)documentedComponentIds.Count / components.Count * 100 : 0,
            ApiCoverage = apiComponents.Count > 0 ? (double)documentedApis / apiComponents.Count * 100 : 0,
            UndocumentedComponents = undocumentedComponents
        });
    }

    public Task<ReadabilityMetrics> AnalyzeReadabilityAsync(List<WikiPage> pages)
    {
        var pageScores = new List<PageReadabilityScore>();

        foreach (var page in pages)
        {
            var score = AnalyzePageReadability(page);
            pageScores.Add(score);
        }

        return Task.FromResult(new ReadabilityMetrics
        {
            PageScores = pageScores,
            AverageReadabilityScore = pageScores.Any() ? pageScores.Average(s => s.Score) : 0,
            AverageWordCount = pageScores.Any() ? pageScores.Average(s => s.WordCount) : 0,
            AverageSectionCount = pageScores.Any() ? pageScores.Average(s => s.SectionCount) : 0
        });
    }

    public Task<BenchmarkReport> GenerateBenchmarkReportAsync(string repositoryPath, DocumentationQualityMetrics metrics)
    {
        var report = new BenchmarkReport
        {
            RepositoryPath = repositoryPath,
            GeneratedAt = DateTime.UtcNow,
            Metrics = metrics
        };

        // Generate comparisons
        report.Comparisons = GenerateBenchmarkComparisons(metrics);

        // Generate summary
        report.Summary = GenerateSummary(metrics);

        // Generate action items
        report.ActionItems = GenerateActionItems(metrics);

        return Task.FromResult(report);
    }

    private PageReadabilityScore AnalyzePageReadability(WikiPage page)
    {
        var issues = new List<string>();
        var score = 100.0;

        // Word count analysis
        var wordCount = CountWords(page.Content);
        if (wordCount < 50)
        {
            issues.Add("Content is too brief (less than 50 words)");
            score -= 20;
        }
        else if (wordCount > 2000)
        {
            issues.Add("Content is too long (more than 2000 words)");
            score -= 10;
        }

        // Section count analysis
        var sectionCount = CountSections(page.Content);
        if (sectionCount < 2)
        {
            issues.Add("Content lacks proper sections");
            score -= 15;
        }

        // Code block analysis
        var codeBlockCount = Regex.Matches(page.Content, @"```").Count / 2;
        if (codeBlockCount == 0 && page.Content.Contains("class") || page.Content.Contains("function"))
        {
            issues.Add("Technical content lacks code examples");
            score -= 10;
        }

        // Link analysis
        var linkCount = Regex.Matches(page.Content, @"\[.*?\]\(.*?\)").Count;
        if (linkCount == 0)
        {
            issues.Add("Content lacks cross-references");
            score -= 5;
        }

        return new PageReadabilityScore
        {
            PageId = page.Id,
            PageTitle = page.Title,
            Score = Math.Max(0, score),
            WordCount = wordCount,
            SectionCount = sectionCount,
            Issues = issues
        };
    }

    private CompletenessMetrics CalculateCompletenessMetrics(List<WikiPage> pages, List<CodeComponent> components)
    {
        var pagesWithExamples = 0;
        var totalDescriptionLength = 0;

        foreach (var page in pages)
        {
            if (Regex.IsMatch(page.Content, @"```"))
                pagesWithExamples++;

            totalDescriptionLength += page.Description.Length;
        }

        return new CompletenessMetrics
        {
            TotalPages = pages.Count,
            PagesWithExamples = pagesWithExamples,
            ExampleCoverage = pages.Any() ? (double)pagesWithExamples / pages.Count * 100 : 0,
            DescriptionCompleteness = pages.Any() ? Math.Min(100, (double)totalDescriptionLength / pages.Count / 50) : 0,
            ParameterDocumentation = 75 // Placeholder - would need deeper analysis
        };
    }

    private AccuracyMetrics CalculateAccuracyMetrics(List<WikiPage> pages, List<CodeComponent> components)
    {
        var inconsistencies = new List<string>();
        var outdatedReferences = new List<string>();

        // Check for consistency in naming conventions
        var pageTitles = pages.Select(p => p.Title).ToList();
        var inconsistentNaming = pageTitles.Where(title => 
            !char.IsUpper(title.FirstOrDefault()) || title.Contains(" "))
            .ToList();

        if (inconsistentNaming.Any())
        {
            inconsistencies.Add($"Inconsistent naming in {inconsistentNaming.Count} pages");
        }

        return new AccuracyMetrics
        {
            ConsistencyScore = Math.Max(0, 100 - inconsistencies.Count * 10),
            CurrencyScore = 85, // Placeholder - would need git history analysis
            Inconsistencies = inconsistencies,
            OutdatedReferences = outdatedReferences
        };
    }

    private double CalculateOverallScore(DocumentationQualityMetrics metrics)
    {
        var weights = new Dictionary<string, double>
        {
            { "Coverage", 0.35 },
            { "Readability", 0.25 },
            { "Completeness", 0.25 },
            { "Accuracy", 0.15 }
        };

        var coverageScore = (metrics.Coverage.ComponentCoverage + metrics.Coverage.ApiCoverage) / 2;
        var readabilityScore = metrics.Readability.AverageReadabilityScore;
        var completenessScore = (metrics.Completeness.DescriptionCompleteness + 
                                metrics.Completeness.ExampleCoverage + 
                                metrics.Completeness.ParameterDocumentation) / 3;
        var accuracyScore = (metrics.Accuracy.ConsistencyScore + metrics.Accuracy.CurrencyScore) / 2;

        return weights["Coverage"] * coverageScore +
               weights["Readability"] * readabilityScore +
               weights["Completeness"] * completenessScore +
               weights["Accuracy"] * accuracyScore;
    }

    private List<string> GenerateRecommendations(DocumentationQualityMetrics metrics)
    {
        var recommendations = new List<string>();

        if (metrics.Coverage.ComponentCoverage < 80)
        {
            recommendations.Add($"Document {metrics.Coverage.UndocumentedComponents.Count} undocumented components");
        }

        if (metrics.Readability.AverageReadabilityScore < 70)
        {
            recommendations.Add("Improve content readability with better structure and examples");
        }

        if (metrics.Completeness.ExampleCoverage < 50)
        {
            recommendations.Add("Add more code examples to documentation");
        }

        if (metrics.Accuracy.ConsistencyScore < 80)
        {
            recommendations.Add("Standardize naming conventions across documentation");
        }

        return recommendations;
    }

    private List<BenchmarkComparison> GenerateBenchmarkComparisons(DocumentationQualityMetrics metrics)
    {
        var comparisons = new List<BenchmarkComparison>();

        var coverageScore = (metrics.Coverage.ComponentCoverage + metrics.Coverage.ApiCoverage) / 2;
        comparisons.Add(CreateComparison("Component Coverage", coverageScore, _industryBenchmarks["ComponentCoverage"]));

        comparisons.Add(CreateComparison("Readability", metrics.Readability.AverageReadabilityScore, _industryBenchmarks["ReadabilityScore"]));
        comparisons.Add(CreateComparison("Description Completeness", metrics.Completeness.DescriptionCompleteness, _industryBenchmarks["DescriptionCompleteness"]));
        comparisons.Add(CreateComparison("Example Coverage", metrics.Completeness.ExampleCoverage, _industryBenchmarks["ExampleCoverage"]));

        return comparisons;
    }

    private BenchmarkComparison CreateComparison(string metric, double currentValue, double industryAverage)
    {
        var percentile = CalculatePercentile(currentValue, industryAverage);
        var status = percentile >= 90 ? "Excellent" : percentile >= 75 ? "Good" : percentile >= 50 ? "Average" : "Poor";

        return new BenchmarkComparison
        {
            Metric = metric,
            CurrentValue = currentValue,
            IndustryAverage = industryAverage,
            Percentile = percentile,
            Status = status
        };
    }

    private double CalculatePercentile(double value, double average)
    {
        // Simple percentile calculation based on deviation from average
        var deviation = value - average;
        var standardDeviation = average == 0 ? 1 : average * 0.2; // Assume 20% standard deviation, avoid division by zero
        var zScore = deviation / standardDeviation;
        
        // Convert z-score to percentile (approximate)
        return Math.Max(0, Math.Min(100, 50 + zScore * 20));
    }

    private string GenerateSummary(DocumentationQualityMetrics metrics)
    {
        var grade = metrics.OverallScore >= 90 ? "Excellent" : 
                   metrics.OverallScore >= 80 ? "Good" : 
                   metrics.OverallScore >= 70 ? "Average" : "Poor";

        return $"Documentation quality is {grade} with an overall score of {metrics.OverallScore:F1}/100. " +
               $"Component coverage is {metrics.Coverage.ComponentCoverage:F1}% and readability score is {metrics.Readability.AverageReadabilityScore:F1}/100.";
    }

    private List<string> GenerateActionItems(DocumentationQualityMetrics metrics)
    {
        var actionItems = new List<string>();

        if (metrics.Coverage.UndocumentedComponents.Any())
        {
            actionItems.Add($"Priority 1: Document {metrics.Coverage.UndocumentedComponents.Count} missing components");
        }

        if (metrics.Completeness.ExampleCoverage < 50)
        {
            actionItems.Add("Priority 2: Add code examples to improve understanding");
        }

        if (metrics.Readability.AverageReadabilityScore < 70)
        {
            actionItems.Add("Priority 3: Restructure content for better readability");
        }

        return actionItems.OrderByDescending(item => item.Contains("Priority 1")).ToList();
    }

    private int CountWords(string content)
    {
        return Regex.Matches(content, @"\b\w+\b").Count;
    }

    private int CountSections(string content)
    {
        return Regex.Matches(content, @"^#+\s", RegexOptions.Multiline).Count;
    }
}