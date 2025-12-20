using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Core.Tests;

[TestFixture]
public class EvaluationMetricsSystemTests
{
    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<EvaluationMetricsSystem>>();
        _mockJudgeAgent = new Mock<IJudgeAgent>();

        var options = new CodeWikiOptions { MaxDegreeOfParallelism = 5 };
        _mockOptions = new Mock<IOptions<CodeWikiOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(options);

        _service = new EvaluationMetricsSystem(_mockLogger.Object, _mockJudgeAgent.Object, _mockOptions.Object);
    }

    private Mock<ILogger<EvaluationMetricsSystem>> _mockLogger;
    private Mock<IJudgeAgent> _mockJudgeAgent;
    private Mock<IOptions<CodeWikiOptions>> _mockOptions;
    private EvaluationMetricsSystem _service;

    [Test]
    public async Task EvaluateDocumentationQualityAsync_ShouldReturnValidMetrics_WhenValidInputProvided()
    {
        // Arrange
        var structure = new WikiStructure { Title = "Test" };
        var pages = new List<WikiPage>
        {
            new() { Id = "TestClass", Title = "Test Class" }
        };
        var components = new List<CodeComponent>
        {
            new()
            {
                Id = "TestClass",
                Name = "TestClass",
                Type = "Class",
                Language = "C#",
                FilePath = "/test/TestClass.cs"
            }
        };

        // Act
        var result = await _service.EvaluateDocumentationQualityAsync(structure, pages, components);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.OverallScore, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.Coverage, Is.Not.Null);
        Assert.That(result.Readability, Is.Not.Null);
        Assert.That(result.Completeness, Is.Not.Null);
        Assert.That(result.Accuracy, Is.Not.Null);
        Assert.That(result.Recommendations, Is.Not.Null);
    }

    [Test]
    public async Task CalculateCoverageMetricsAsync_ShouldReturnFullCoverage_WhenAllComponentsDocumented()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new() { Id = "Component1", Title = "Component 1" },
            new() { Id = "Component2", Title = "Component 2" }
        };

        var components = new List<CodeComponent>
        {
            new() { Id = "Component1", Name = "Component1", Type = "Class" },
            new() { Id = "Component2", Name = "Component2", Type = "Service" }
        };

        // Act
        var result = await _service.CalculateCoverageMetricsAsync(pages, components);

        // Assert
        Assert.That(result.ComponentCoverage, Is.EqualTo(100.0));
        Assert.That(result.TotalComponents, Is.EqualTo(2));
        Assert.That(result.DocumentedComponents, Is.EqualTo(2));
        Assert.That(result.UndocumentedComponents, Is.Empty);
    }

    [Test]
    public async Task CalculateCoverageMetricsAsync_ShouldReturnPartialCoverage_WhenSomeComponentsMissing()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new() { Id = "Component1", Title = "Component 1" }
        };

        var components = new List<CodeComponent>
        {
            new() { Id = "Component1", Name = "Component1", Type = "Class" },
            new() { Id = "Component2", Name = "Component2", Type = "Class" },
            new() { Id = "ApiController", Name = "ApiController", Type = "Controller" }
        };

        // Act
        var result = await _service.CalculateCoverageMetricsAsync(pages, components);

        // Assert
        Assert.That(Math.Round(result.ComponentCoverage, 2), Is.EqualTo(33.33));
        Assert.That(result.TotalComponents, Is.EqualTo(3));
        Assert.That(result.DocumentedComponents, Is.EqualTo(1));
        Assert.That(result.UndocumentedComponents, Does.Contain("Component2"));
        Assert.That(result.UndocumentedComponents, Does.Contain("ApiController"));
    }

    [Test]
    public async Task AnalyzeReadabilityAsync_ShouldReturnHighScore_ForWellStructuredContent()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new()
            {
                Id = "WellStructuredPage",
                Title = "Well Structured Page",
                Description = "A well structured documentation page",
                Content = @"# Well Structured Page

This page demonstrates good documentation practices.

## Overview

Here we explain the purpose and usage.

## Methods

### Method1()

This method does something important.

### Method2(string parameter)

This method takes a parameter.

## Examples

```csharp
var instance = new WellStructuredPage();
instance.Method1();
```

## See Also

- [Related Page](related-page.md)
- [API Reference](api-reference.md)"
            }
        };

        // Act
        var result = await _service.AnalyzeReadabilityAsync(pages);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AverageReadabilityScore, Is.GreaterThanOrEqualTo(80));
        Assert.That(result.AverageWordCount, Is.GreaterThan(50));
        Assert.That(result.AverageSectionCount, Is.GreaterThanOrEqualTo(3));
        Assert.That(result.PageScores.Count, Is.EqualTo(1));

        var pageScore = result.PageScores.First();
        Assert.That(pageScore.PageId, Is.EqualTo("WellStructuredPage"));
        Assert.That(pageScore.Score, Is.GreaterThanOrEqualTo(80));
        Assert.That(pageScore.Issues, Is.Empty);
    }

    [Test]
    public async Task AnalyzeReadabilityAsync_ShouldReturnLowScore_ForPoorlyStructuredContent()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new()
            {
                Id = "PoorPage",
                Title = "Poor Page",
                Description = "A poorly structured page",
                Content = "short content without structure or examples"
            }
        };

        // Act
        var result = await _service.AnalyzeReadabilityAsync(pages);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AverageReadabilityScore, Is.LessThan(70));
        Assert.That(result.AverageWordCount, Is.LessThan(50));
        Assert.That(result.AverageSectionCount, Is.EqualTo(0));

        var pageScore = result.PageScores.First();
        Assert.That(pageScore.PageId, Is.EqualTo("PoorPage"));
        Assert.That(pageScore.Score, Is.LessThan(70));
        Assert.That(pageScore.Issues, Is.Not.Empty);
        Assert.That(pageScore.Issues, Has.Some.Matches<string>(issue => issue.Contains("too brief")));
        Assert.That(pageScore.Issues, Has.Some.Matches<string>(issue => issue.Contains("lacks proper sections")));
    }

    [Test]
    public async Task GenerateBenchmarkReportAsync_ShouldReturnComprehensiveReport()
    {
        // Arrange
        var metrics = new DocumentationQualityMetrics
        {
            OverallScore = 85.5,
            Coverage = new CoverageMetrics
            {
                ComponentCoverage = 90.0,
                ApiCoverage = 80.0,
                TotalComponents = 10,
                DocumentedComponents = 9,
                UndocumentedComponents = new List<string> { "MissingComponent" }
            },
            Readability = new ReadabilityMetrics
            {
                AverageReadabilityScore = 75.0,
                AverageWordCount = 150,
                AverageSectionCount = 4
            },
            Completeness = new CompletenessMetrics
            {
                DescriptionCompleteness = 85.0,
                ExampleCoverage = 70.0,
                ParameterDocumentation = 80.0
            },
            Accuracy = new AccuracyMetrics
            {
                ConsistencyScore = 90.0,
                CurrencyScore = 85.0
            }
        };

        var repositoryPath = "/test/repo";

        // Act
        var result = await _service.GenerateBenchmarkReportAsync(repositoryPath, metrics);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RepositoryPath, Is.EqualTo(repositoryPath));
        Assert.That(result.Metrics, Is.EqualTo(metrics));
        Assert.That(result.Comparisons, Is.Not.Null);
        Assert.That(result.Comparisons.Count, Is.GreaterThanOrEqualTo(4));
        Assert.That(result.Summary, Is.Not.Empty);

        // Check that comparisons include expected metrics
        var coverageComparison = result.Comparisons.FirstOrDefault(c => c.Metric == "Component Coverage");
        Assert.That(coverageComparison, Is.Not.Null);
        Assert.That(coverageComparison.CurrentValue, Is.EqualTo(90.0)); // ComponentCoverage value
        Assert.That(coverageComparison.Status, Is.Not.Empty);
    }

    [TestCase(95.0, "Excellent")] // 95 vs 85 benchmark = 111.76 percentile >=110
    [TestCase(85.0, "Good")] // 85 vs 85 benchmark = 100 percentile >=90
    [TestCase(70.0, "Average")] // 70 vs 85 benchmark = 82.35 percentile >=70
    [TestCase(45.0, "Poor")] // 45 vs 85 benchmark = 52.94 percentile <70
    public async Task GenerateBenchmarkReportAsync_ShouldClassifyStatusCorrectly(double score, string expectedStatus)
    {
        // Arrange
        var metrics = new DocumentationQualityMetrics
        {
            OverallScore = score,
            Coverage = new CoverageMetrics { ComponentCoverage = score, ApiCoverage = score },
            Readability = new ReadabilityMetrics { AverageReadabilityScore = score },
            Completeness = new CompletenessMetrics { DescriptionCompleteness = score },
            Accuracy = new AccuracyMetrics { ConsistencyScore = score }
        };

        // Act
        var result = await _service.GenerateBenchmarkReportAsync("/test/repo", metrics);

        // Assert
        var coverageComparison = result.Comparisons.FirstOrDefault(c => c.Metric == "Component Coverage");
        Assert.That(coverageComparison, Is.Not.Null);
        Assert.That(coverageComparison.CurrentValue, Is.EqualTo(score));
        Assert.That(coverageComparison.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public async Task EvaluateDocumentationQualityAsync_ShouldGenerateRecommendations_WhenQualityIsLow()
    {
        // Arrange
        var structure = new WikiStructure { Title = "Test" };
        var pages = new List<WikiPage>(); // No pages
        var components = new List<CodeComponent>
        {
            new() { Id = "Component1", Name = "Component1", Type = "Class" }
        };

        // Act
        var result = await _service.EvaluateDocumentationQualityAsync(structure, pages, components);

        // Assert
        Assert.That(result.Recommendations, Is.Not.Null);
        Assert.That(result.Recommendations.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(result.Recommendations, Has.Some.Matches<string>(r => r.Contains("documentation")));
        Assert.That(result.OverallScore, Is.LessThan(70)); // Should be low due to missing documentation
    }
}