using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly SemaphoreSlim _semaphore;

    public EvaluationMetricsSystem(
        ILogger<EvaluationMetricsSystem> logger,
        IJudgeAgent judgeAgent,
        IOptions<CodeWikiOptions> options)
    {
        _logger = logger;
        _judgeAgent = judgeAgent;
        _semaphore = new SemaphoreSlim(options.Value.MaxDegreeOfParallelism);
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

        _logger.LogInformation("Starting single judge-based evaluation for page: {PageTitle}", page.Title);

        var breakdown = new ConcurrentDictionary<string, RequirementScore>();
        var scoresByCategory = new ConcurrentDictionary<string, ConcurrentBag<double>>();

        var (overallScore, overallUncertainty) =
            await EvaluateRubricNodeAsync(page, rubric, breakdown, scoresByCategory);

        var (reliability, stdDeviation) = CalculateReliabilityMetrics(scoresByCategory);

        var qualityScore = new QualityScore
        {
            OverallScore = overallScore,
            Breakdown = new Dictionary<string, RequirementScore>(breakdown),
            Reliability = reliability,
            StandardDeviation = stdDeviation,
            Uncertainty = overallUncertainty
        };

        _logger.LogInformation(
            "Single judge-based evaluation completed with score: {Score}, reliability: {Reliability}, uncertainty: {Uncertainty}",
            overallScore, reliability, overallUncertainty);

        return qualityScore;
    }

    public async Task<ConsensusQualityScore> EvaluateWithMultipleJudgesAsync(WikiPage page, EvaluationRubric rubric,
        List<IJudgeAgent> judges)
    {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (rubric == null) throw new ArgumentNullException(nameof(rubric));
        if (judges == null || !judges.Any())
            throw new ArgumentException("At least one judge agent must be provided", nameof(judges));

        _logger.LogInformation(
            "Starting multi-judge consensus evaluation for page: {PageTitle} with {JudgeCount} judges",
            page.Title, judges.Count);

        const int MINIMUM_JUDGES_REQUIRED = 3;
        var meetsMinimumRequirement = judges.Count >= MINIMUM_JUDGES_REQUIRED;

        var judgeTasks = new List<Task<(string JudgeId, QualityScore? Score)>>();

        for (var i = 0; i < judges.Count; i++)
        {
            var judge = judges[i];
            var judgeId = $"Judge_{i + 1}";
            judgeTasks.Add(EvaluateJudgeSafeAsync(page, rubric, judge, judgeId));
        }

        var results = await Task.WhenAll(judgeTasks);
        var validResults = results.Where(r => r.Score != null).ToList();

        if (!validResults.Any()) throw new InvalidOperationException("All judge evaluations failed");

        var individualScores = new List<IndividualJudgeScore>();
        var judgeReliabilities = new Dictionary<string, double>();

        foreach (var (judgeId, score) in validResults)
        {
            var individualScore = new IndividualJudgeScore
            {
                JudgeId = judgeId,
                OverallScore = score!.OverallScore,
                Breakdown = score.Breakdown ?? new Dictionary<string, RequirementScore>(),
                Reliability = score.Reliability,
                StandardDeviation = score.StandardDeviation ?? new Dictionary<string, double>(),
                OverallUncertainty = score.Uncertainty
            };

            individualScores.Add(individualScore);
            judgeReliabilities[judgeId] = score.Reliability;

            _logger.LogInformation("{JudgeId} completed evaluation with score: {Score}, reliability: {Reliability}",
                judgeId, score.OverallScore, score.Reliability);
        }

        // Calculate consensus metrics
        var consensusScore = CalculateConsensusScore(individualScores);
        var consensusStatus = DetermineConsensusStatus(individualScores, meetsMinimumRequirement);

        var consensusQualityScore = new ConsensusQualityScore
        {
            OverallScore = consensusScore.WeightedAverageScore,
            JudgeCount = individualScores.Count,
            JudgeReliabilities = judgeReliabilities,
            ConsensusScore = consensusScore.ConsensusValue,
            IndividualScores = individualScores,
            MeetsMinimumJudgeRequirement = meetsMinimumRequirement,
            ConsensusStatus = consensusStatus,
            Reliability = consensusScore.OverallReliability,
            StandardDeviation = consensusScore.CategoryStandardDeviations,
            OverallUncertainty = consensusScore.OverallUncertainty,
            Uncertainty = consensusScore.OverallUncertainty
        };

        // Aggregate breakdown scores across all judges
        consensusQualityScore.Breakdown = AggregateJudgeBreakdowns(individualScores);

        _logger.LogInformation(
            "Multi-judge consensus evaluation completed. Overall: {OverallScore}, Consensus: {ConsensusScore}, Status: {Status}",
            consensusQualityScore.OverallScore, consensusQualityScore.ConsensusScore,
            consensusQualityScore.ConsensusStatus);

        return consensusQualityScore;
    }

    private async Task<(string JudgeId, QualityScore? Score)> EvaluateJudgeSafeAsync(WikiPage page,
        EvaluationRubric rubric, IJudgeAgent judge, string judgeId)
    {
        try
        {
            _logger.LogInformation("Evaluating with {JudgeId}", judgeId);
            var score = await EvaluateWithSpecificJudgeAsync(page, rubric, judge);
            return (judgeId, score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating with {JudgeId}", judgeId);
            return (judgeId, null);
        }
    }

    private async Task<QualityScore> EvaluateWithSpecificJudgeAsync(WikiPage page, EvaluationRubric rubric,
        IJudgeAgent judge)
    {
        var breakdown = new ConcurrentDictionary<string, RequirementScore>();
        var scoresByCategory = new ConcurrentDictionary<string, ConcurrentBag<double>>();

        var overallScore = await EvaluateRubricNodeWithJudgeAsync(page, rubric, breakdown, scoresByCategory, judge);

        var (reliability, stdDeviation) = CalculateReliabilityMetrics(scoresByCategory);

        return new QualityScore
        {
            OverallScore = overallScore,
            Breakdown = new Dictionary<string, RequirementScore>(breakdown),
            Reliability = reliability,
            StandardDeviation = stdDeviation
        };
    }

    private async Task<double> EvaluateRubricNodeWithJudgeAsync(
        WikiPage page,
        RubricNode node,
        ConcurrentDictionary<string, RequirementScore> breakdown,
        ConcurrentDictionary<string, ConcurrentBag<double>> scoresByCategory,
        IJudgeAgent judge)
    {
        if (node is RubricRequirement requirement)
        {
            RequirementScore score;
            await _semaphore.WaitAsync();
            try
            {
                score = await judge.EvaluateRequirementAsync(page, requirement);
            }
            finally
            {
                _semaphore.Release();
            }

            breakdown[requirement.Title] = score;

            // Only include successful evaluations in score aggregations
            if (!score.EvaluationFailed)
            {
                var category = GetParentCategory(node);
                var categoryScores = scoresByCategory.GetOrAdd(category, _ => new ConcurrentBag<double>());
                categoryScores.Add(score.Score);

                return score.Score;
            }

            // Return -1 to signal this should be excluded from weighted average
            return -1.0;
        }

        if (node.Children != null && node.Children.Any())
        {
            var tasks = node.Children.Select(async child =>
            {
                var s = await EvaluateRubricNodeWithJudgeAsync(page, child, breakdown, scoresByCategory, judge);
                return (Score: s, child.Weight);
            });

            var results = await Task.WhenAll(tasks);

            var childScores = new List<double>();
            var childWeights = new List<double>();

            foreach (var result in results)
                // Only include successful evaluations
                if (result.Score >= 0)
                {
                    childScores.Add(result.Score);
                    childWeights.Add(result.Weight);
                }

            return childScores.Any() ? CalculateWeightedAverage(childScores, childWeights) : 0.0;
        }

        return 0.0;
    }

    private (double WeightedAverageScore, double ConsensusValue, double OverallReliability, Dictionary<string, double>
        CategoryStandardDeviations, double OverallUncertainty)
        CalculateConsensusScore(List<IndividualJudgeScore> individualScores)
    {
        if (!individualScores.Any())
            return (0.0, 0.0, 0.0, new Dictionary<string, double>(), 0.0);

        var scores = individualScores.Select(s => s.OverallScore).ToList();
        var reliabilities = individualScores.Select(s => s.Reliability).ToList();
        var uncertainties = individualScores.Select(s => s.OverallUncertainty).ToList();

        // Calculate reliability-weighted average score
        var totalReliabilityWeight = reliabilities.Sum();
        var weightedAverageScore = totalReliabilityWeight > 0
            ? scores.Zip(reliabilities, (score, reliability) => score * reliability).Sum() / totalReliabilityWeight
            : scores.Average();

        // Calculate consensus value based on score agreement
        var meanScore = scores.Average();
        var scoreVariance = scores.Select(s => Math.Pow(s - meanScore, 2)).Average();
        var scoreStandardDeviation = Math.Sqrt(scoreVariance);
        var scoreRange = scores.Max() - scores.Min();

        // Consensus value: 1.0 = perfect agreement, 0.0 = no agreement
        var consensusValue = scoreRange > 0 ? Math.Max(0, 1.0 - scoreStandardDeviation / scoreRange) : 1.0;

        // Overall reliability combines individual reliabilities with consensus
        var averageIndividualReliability = reliabilities.Average();
        var overallReliability = averageIndividualReliability * consensusValue;

        // Calculate category-level standard deviations
        var categoryStandardDeviations = CalculateCategoryStandardDeviations(individualScores);

        // Calculate overall uncertainty from judge disagreement
        // Use the standard deviation of scores as the overall uncertainty
        var overallUncertainty = scoreStandardDeviation;

        return (weightedAverageScore, consensusValue, overallReliability, categoryStandardDeviations,
            overallUncertainty);
    }

    private Dictionary<string, double> CalculateCategoryStandardDeviations(List<IndividualJudgeScore> individualScores)
    {
        var categoryStandardDeviations = new Dictionary<string, double>();

        if (!individualScores.Any())
            return categoryStandardDeviations;

        // Collect all requirement categories across all judges
        var allCategories = individualScores
            .SelectMany(score => score.Breakdown.Keys)
            .Distinct()
            .ToList();

        foreach (var category in allCategories)
        {
            var categoryScores = individualScores
                .Where(score => score.Breakdown.ContainsKey(category))
                .Select(score => score.Breakdown[category].Score)
                .ToList();

            if (categoryScores.Count > 1)
            {
                var mean = categoryScores.Average();
                var variance = categoryScores.Select(s => Math.Pow(s - mean, 2)).Average();
                var stdDev = Math.Sqrt(variance);
                categoryStandardDeviations[category] = stdDev;
            }
            else
            {
                categoryStandardDeviations[category] = 0.0;
            }
        }

        return categoryStandardDeviations;
    }

    private string DetermineConsensusStatus(List<IndividualJudgeScore> individualScores, bool meetsMinimumRequirement)
    {
        if (!meetsMinimumRequirement)
            return "Insufficient Judges (Minimum 3 Required)";

        if (!individualScores.Any())
            return "No Valid Evaluations";

        var scores = individualScores.Select(s => s.OverallScore).ToList();
        var scoreRange = scores.Max() - scores.Min();

        if (scoreRange <= 10.0) // High agreement
            return "Strong Consensus";
        if (scoreRange <= 25.0) // Moderate agreement
            return "Moderate Consensus";
        // Low agreement
        return "Low Consensus";
    }

    private Dictionary<string, RequirementScore> AggregateJudgeBreakdowns(List<IndividualJudgeScore> individualScores)
    {
        var aggregatedBreakdown = new Dictionary<string, RequirementScore>();

        if (!individualScores.Any())
            return aggregatedBreakdown;

        // Get all unique requirement titles across all judges
        var allRequirementTitles = individualScores
            .SelectMany(score => score.Breakdown.Keys)
            .Distinct()
            .ToList();

        foreach (var requirementTitle in allRequirementTitles)
        {
            var requirementScores = individualScores
                .Where(score => score.Breakdown.ContainsKey(requirementTitle))
                .Select(score => score.Breakdown[requirementTitle])
                .ToList();

            if (requirementScores.Any())
            {
                var averageScore = requirementScores.Average(rs => rs.Score);
                var combinedReasoning = string.Join(" | ",
                    requirementScores.Select(rs => rs.Reasoning).Where(r => !string.IsNullOrWhiteSpace(r)));

                aggregatedBreakdown[requirementTitle] = new RequirementScore
                {
                    RequirementId = requirementTitle,
                    Score = averageScore,
                    Reasoning = $"Aggregated from {requirementScores.Count} judges: {combinedReasoning}",
                    Uncertainty = 0.0 // Uncertainty is tracked at the category level, not individual requirement level
                };
            }
        }

        return aggregatedBreakdown;
    }

    private async Task<(double Score, double Uncertainty)> EvaluateRubricNodeAsync(
        WikiPage page,
        RubricNode node,
        ConcurrentDictionary<string, RequirementScore> breakdown,
        ConcurrentDictionary<string, ConcurrentBag<double>> scoresByCategory)
    {
        if (node is RubricRequirement requirement)
        {
            RequirementScore score;
            await _semaphore.WaitAsync();
            try
            {
                score = await _judgeAgent.EvaluateRequirementAsync(page, requirement);
            }
            finally
            {
                _semaphore.Release();
            }

            breakdown[requirement.Title] = score;

            var category = GetParentCategory(node);
            var categoryScores = scoresByCategory.GetOrAdd(category, _ => new ConcurrentBag<double>());
            categoryScores.Add(score.Score);

            // Return score and its uncertainty (from the requirement evaluation)
            return (score.Score, score.Uncertainty);
        }

        if (node.Children != null && node.Children.Any())
        {
            var tasks = node.Children.Select(async child =>
            {
                var (s, u) = await EvaluateRubricNodeAsync(page, child, breakdown, scoresByCategory);
                return (Score: s, Uncertainty: u, child.Weight);
            });

            var results = await Task.WhenAll(tasks);

            var childScores = new List<double>();
            var childUncertainties = new List<double>();
            var childWeights = new List<double>();

            foreach (var result in results)
            {
                childScores.Add(result.Score);
                childUncertainties.Add(result.Uncertainty);
                childWeights.Add(result.Weight);
            }

            var weightedScore = CalculateWeightedAverage(childScores, childWeights);
            var propagatedUncertainty = CalculatePropagatedUncertainty(childUncertainties, childWeights);

            return (weightedScore, propagatedUncertainty);
        }

        return (0.0, 0.0);
    }

    private static double CalculateWeightedAverage(List<double> scores, List<double> weights)
    {
        if (!scores.Any() || !weights.Any() || scores.Count != weights.Count) return 0.0;

        var totalWeight = weights.Sum();
        if (totalWeight <= 0.0) return 0.0;

        var weightedSum = scores.Select((score, index) => score * weights[index]).Sum();
        return weightedSum / totalWeight;
    }

    /// <summary>
    ///     Propagates uncertainty from child nodes to parent using weighted quadrature sum.
    ///     Formula: σ_parent = sqrt(Σ(w_i² * σ_i²)) / Σ(w_i)
    ///     This follows standard uncertainty propagation for weighted averages.
    /// </summary>
    private static double CalculatePropagatedUncertainty(List<double> uncertainties, List<double> weights)
    {
        if (!uncertainties.Any() || !weights.Any() || uncertainties.Count != weights.Count) return 0.0;

        var totalWeight = weights.Sum();
        if (totalWeight <= 0.0) return 0.0;

        // Weighted quadrature sum: sqrt(Σ(w_i² * σ_i²)) / Σ(w_i)
        var weightedVarianceSum = uncertainties
            .Select((uncertainty, index) => weights[index] * weights[index] * uncertainty * uncertainty)
            .Sum();

        return Math.Sqrt(weightedVarianceSum) / totalWeight;
    }

    private static (double Reliability, Dictionary<string, double> StandardDeviation)
        CalculateReliabilityMetrics(ConcurrentDictionary<string, ConcurrentBag<double>> scoresByCategory)
    {
        var stdDeviation = new Dictionary<string, double>();
        var reliabilities = new List<double>();

        foreach (var (category, bag) in scoresByCategory)
        {
            var scores = bag.ToList();
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