using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class EvaluationMetricsSystem : IEvaluationMetricsSystem
{
    private readonly Dictionary<string, double> _industryBenchmarks = new()
    {
        { "ComponentCoverage", 85.0 },
        { "ApiCoverage", 90.0 },
        { "ReadabilityScore", 75.0 },
        { "DescriptionCompleteness", 80.0 },
        { "ExampleCoverage", 60.0 }
    };

    private readonly IJudgeAgent _judgeAgent;
    private readonly ILogger<EvaluationMetricsSystem> _logger;

    public EvaluationMetricsSystem(ILogger<EvaluationMetricsSystem> logger, IJudgeAgent judgeAgent)
    {
        _logger = logger;
        _judgeAgent = judgeAgent;
    }

    public async Task<DocumentationQualityMetrics> EvaluateDocumentationQualityAsync(
        WikiStructure structure,
        List<WikiPage> pages,
        List<CodeComponent> components)
    {
        _logger.LogInformation("Starting documentation quality evaluation");

        var metrics = new DocumentationQualityMetrics();

        metrics.Coverage = await CalculateCoverageMetricsAsync(pages, components);
        metrics.Readability = await AnalyzeReadabilityAsync(pages);
        metrics.Completeness = CalculateCompletenessMetrics(pages, components);
        metrics.Accuracy = CalculateAccuracyMetrics(pages, components);
        metrics.OverallScore = CalculateOverallScore(metrics);
        metrics.Recommendations = GenerateRecommendations(metrics);

        _logger.LogInformation("Documentation quality evaluation completed with overall score: {Score}",
            metrics.OverallScore);
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
            ComponentCoverage =
                components.Count > 0 ? (double)documentedComponentIds.Count / components.Count * 100 : 0,
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

    public Task<BenchmarkReport> GenerateBenchmarkReportAsync(string repositoryPath,
        DocumentationQualityMetrics metrics)
    {
        var comparisons = new List<BenchmarkComparison>
        {
            CreateComparison("Component Coverage", metrics.Coverage.ComponentCoverage,
                _industryBenchmarks["ComponentCoverage"]),
            CreateComparison("API Coverage", metrics.Coverage.ApiCoverage, _industryBenchmarks["ApiCoverage"]),
            CreateComparison("Readability", metrics.Readability.AverageReadabilityScore,
                _industryBenchmarks["ReadabilityScore"]),
            CreateComparison("Examples", metrics.Completeness.ExampleCoverage, _industryBenchmarks["ExampleCoverage"])
        };

        var report = new BenchmarkReport
        {
            RepositoryPath = repositoryPath,
            GeneratedAt = DateTime.Now,
            Metrics = metrics,
            Comparisons = comparisons,
            Summary = GenerateBenchmarkSummary(metrics, comparisons),
            ActionItems = metrics.Recommendations
        };

        return Task.FromResult(report);
    }

    public async Task<QualityScore> EvaluateWithJudgesAsync(WikiPage page, EvaluationRubric rubric)
    {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (rubric == null) throw new ArgumentNullException(nameof(rubric));

        _logger.LogInformation("Starting judge-based evaluation for page: {PageTitle}", page.Title);

        var breakdown = new Dictionary<string, RequirementScore>();
        var scoresByCategory = new Dictionary<string, List<double>>();

        var overallScore = await EvaluateRubricNodeAsync(page, rubric, breakdown, scoresByCategory);

        var (reliability, stdDeviation) = CalculateReliabilityMetrics(scoresByCategory);

        var qualityScore = new QualityScore
        {
            OverallScore = overallScore,
            Breakdown = breakdown,
            Reliability = reliability,
            StandardDeviation = stdDeviation
        };

        _logger.LogInformation("Judge-based evaluation completed with score: {Score} and reliability: {Reliability}",
            overallScore, reliability);

        return qualityScore;
    }

    private async Task<double> EvaluateRubricNodeAsync(
        WikiPage page,
        RubricNode node,
        Dictionary<string, RequirementScore> breakdown,
        Dictionary<string, List<double>> scoresByCategory)
    {
            if (node is RubricRequirement requirement)
        {
            var score = await _judgeAgent.EvaluateRequirementAsync(page, requirement);
            breakdown[requirement.Title] = score;

            var category = GetParentCategory(node);
            if (!scoresByCategory.ContainsKey(category))
                scoresByCategory[category] = new List<double>();
            scoresByCategory[category].Add(score.Score);

            return score.Score;
        }

        if (node.Children != null && node.Children.Any())
        {
            var childScores = new List<double>();
            var childWeights = new List<double>();

            foreach (var child in node.Children)
            {
                var childScore = await EvaluateRubricNodeAsync(page, child, breakdown, scoresByCategory);
                childScores.Add(childScore);
                childWeights.Add(child.Weight);
            }

            return CalculateWeightedAverage(childScores, childWeights);
        }

        return 0.0;
    }

    private static double CalculateWeightedAverage(List<double> scores, List<double> weights)
    {
        if (!scores.Any() || !weights.Any() || scores.Count != weights.Count)
            return 0.0;

        var totalWeight = weights.Sum();
        if (totalWeight <= 0.0)
            return 0.0;

        var weightedSum = scores.Select((score, index) => score * weights[index]).Sum();
        return weightedSum / totalWeight;
    }

    private static (double Reliability, Dictionary<string, double> StandardDeviation)
        CalculateReliabilityMetrics(Dictionary<string, List<double>> scoresByCategory)
    {
        var stdDeviation = new Dictionary<string, double>();
        var reliabilities = new List<double>();

        foreach (var (category, scores) in scoresByCategory)
            if (scores.Count > 1)
            {
                var mean = scores.Average();
                var variance = scores.Select(s => Math.Pow(s - mean, 2)).Average();
                var stdDev = Math.Sqrt(variance);
                stdDeviation[category] = stdDev;

                var range = scores.Max() - scores.Min();
                var reliability = range > 0 ? 1.0 - stdDev / range : 1.0;
                reliabilities.Add(reliability);
            }
            else
            {
                stdDeviation[category] = 0.0;
                reliabilities.Add(1.0);
            }

        var overallReliability = reliabilities.Any() ? reliabilities.Average() : 1.0;
        return (overallReliability, stdDeviation);
    }

    private static string GetParentCategory(RubricNode node)
    {
        return node is RubricRequirement ? "Requirements" : node.Title;
    }

    private PageReadabilityScore AnalyzePageReadability(WikiPage page)
    {
        var issues = new List<string>();
        var score = 100.0;

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

        var sectionCount = CountSections(page.Content);
        if (sectionCount < 2)
        {
            issues.Add("Content lacks proper sections");
            score -= 15;
        }

        var codeBlockCount = Regex.Matches(page.Content, @"```").Count / 2;
        if ((codeBlockCount == 0 && page.Content.Contains("class")) || page.Content.Contains("function"))
        {
            issues.Add("Technical content lacks code examples");
            score -= 10;
        }

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

    private int CountWords(string content)
    {
        return Regex.Matches(content, @"\b\w+\b").Count;
    }

    private int CountSections(string content)
    {
        return Regex.Matches(content, @"^#+\s", RegexOptions.Multiline).Count;
    }

    private CompletenessMetrics CalculateCompletenessMetrics(List<WikiPage> pages, List<CodeComponent> components)
    {
        var totalPages = pages.Count;
        var pagesWithExamples = pages.Count(p => p.Content.Contains("```"));
        var pagesWithParams = pages.Count(p =>
            p.Content.ToLower().Contains("parameter") || p.Content.ToLower().Contains("param"));

        return new CompletenessMetrics
        {
            TotalPages = totalPages,
            PagesWithExamples = pagesWithExamples,
            ExampleCoverage = totalPages > 0 ? (double)pagesWithExamples / totalPages * 100 : 0,
            ParameterDocumentation = totalPages > 0 ? (double)pagesWithParams / totalPages * 100 : 0,
            DescriptionCompleteness = totalPages > 0
                ? pages.Average(p =>
                    string.IsNullOrWhiteSpace(p.Content) ? 0.0 : Math.Min(100.0, (double)CountWords(p.Content) / 10))
                : 0
        };
    }

    private AccuracyMetrics CalculateAccuracyMetrics(List<WikiPage> pages, List<CodeComponent> components)
    {
        var inconsistencies = new List<string>();
        var outdatedReferences = new List<string>();

        var consistencyScore = 90.0; // Simplified calculation
        var currencyScore = 85.0; // Simplified calculation

        return new AccuracyMetrics
        {
            ConsistencyScore = consistencyScore,
            CurrencyScore = currencyScore,
            Inconsistencies = inconsistencies,
            OutdatedReferences = outdatedReferences
        };
    }

    private double CalculateOverallScore(DocumentationQualityMetrics metrics)
    {
        return metrics.Coverage.ComponentCoverage * 0.3 +
               metrics.Readability.AverageReadabilityScore * 0.3 +
               metrics.Completeness.DescriptionCompleteness * 0.2 +
               metrics.Accuracy.ConsistencyScore * 0.2;
    }

    private List<string> GenerateRecommendations(DocumentationQualityMetrics metrics)
    {
        var recommendations = new List<string>();

        if (metrics.Coverage.ComponentCoverage < 80)
            recommendations.Add("Increase component documentation coverage");

        if (metrics.Readability.AverageReadabilityScore < 70)
            recommendations.Add("Improve documentation readability and structure");

        if (metrics.Completeness.ExampleCoverage < 50)
            recommendations.Add("Add more code examples throughout documentation");

        if (metrics.Accuracy.ConsistencyScore < 85)
            recommendations.Add("Ensure consistency in terminology and descriptions");

        return recommendations;
    }

    private BenchmarkComparison CreateComparison(string metric, double currentValue, double industryAverage)
    {
        var percentile = currentValue / industryAverage * 100;
        var status = percentile switch
        {
            >= 110 => "Excellent",
            >= 90 => "Good",
            >= 70 => "Average",
            _ => "Poor"
        };

        return new BenchmarkComparison
        {
            Metric = metric,
            CurrentValue = currentValue,
            IndustryAverage = industryAverage,
            Percentile = Math.Min(percentile, 150), // Cap at 150%
            Status = status
        };
    }

    private string GenerateBenchmarkSummary(DocumentationQualityMetrics metrics, List<BenchmarkComparison> comparisons)
    {
        var excellentCount = comparisons.Count(c => c.Status == "Excellent");
        var poorCount = comparisons.Count(c => c.Status == "Poor");

        if (excellentCount >= 3)
            return "Excellent documentation quality - exceeds industry standards in multiple areas";
        if (poorCount >= 2)
            return "Poor documentation quality - needs significant improvement";

        return "Average documentation quality - meets industry standards but has room for improvement";
    }
}