using Microsoft.Extensions.Logging;
using Moq;
using codeMRI.Core.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Tests;

public class EvaluationMetricsSystemTests
{
    private readonly Mock<ILogger<EvaluationMetricsSystem>> _mockLogger;
    private readonly EvaluationMetricsSystem _service;

    public EvaluationMetricsSystemTests()
    {
        _mockLogger = new Mock<ILogger<EvaluationMetricsSystem>>();
        _service = new EvaluationMetricsSystem(_mockLogger.Object);
    }

    [Fact]
    public async Task EvaluateDocumentationQualityAsync_ShouldReturnMetrics_WhenValidDataProvided()
    {
        // Arrange
        var structure = new WikiStructure
        {
            Title = "Test Documentation",
            Description = "Test documentation description"
        };

        var pages = new List<WikiPage>
        {
            new WikiPage
            {
                Id = "TestClass",
                Title = "Test Class",
                Description = "A test class for demonstration",
                Content = @"# Test Class

This is a test class that demonstrates functionality.

## Methods

- `TestMethod()`: A test method

## Example

```csharp
var test = new TestClass();
test.TestMethod();
```"
            }
        };

        var components = new List<CodeComponent>
        {
            new CodeComponent
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
        Assert.NotNull(result);
        Assert.True(result.OverallScore >= 0);
        Assert.NotNull(result.Coverage);
        Assert.NotNull(result.Readability);
        Assert.NotNull(result.Completeness);
        Assert.NotNull(result.Accuracy);
        Assert.NotNull(result.Recommendations);
    }

    [Fact]
    public async Task CalculateCoverageMetricsAsync_ShouldReturnFullCoverage_WhenAllComponentsDocumented()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new WikiPage { Id = "Component1", Title = "Component 1" },
            new WikiPage { Id = "Component2", Title = "Component 2" }
        };

        var components = new List<CodeComponent>
        {
            new CodeComponent { Id = "Component1", Name = "Component1", Type = "Class" },
            new CodeComponent { Id = "Component2", Name = "Component2", Type = "Service" }
        };

        // Act
        var result = await _service.CalculateCoverageMetricsAsync(pages, components);

        // Assert
        Assert.Equal(100.0, result.ComponentCoverage);
        Assert.Equal(2, result.TotalComponents);
        Assert.Equal(2, result.DocumentedComponents);
        Assert.Empty(result.UndocumentedComponents);
    }

    [Fact]
    public async Task CalculateCoverageMetricsAsync_ShouldReturnPartialCoverage_WhenSomeComponentsMissing()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new WikiPage { Id = "Component1", Title = "Component 1" }
        };

        var components = new List<CodeComponent>
        {
            new CodeComponent { Id = "Component1", Name = "Component1", Type = "Class" },
            new CodeComponent { Id = "Component2", Name = "Component2", Type = "Class" },
            new CodeComponent { Id = "ApiController", Name = "ApiController", Type = "Controller" }
        };

        // Act
        var result = await _service.CalculateCoverageMetricsAsync(pages, components);

        // Assert
        Assert.Equal(33.33, Math.Round(result.ComponentCoverage, 2));
        Assert.Equal(3, result.TotalComponents);
        Assert.Equal(1, result.DocumentedComponents);
        Assert.Contains("Component2", result.UndocumentedComponents);
        Assert.Contains("ApiController", result.UndocumentedComponents);
    }

    [Fact]
    public async Task AnalyzeReadabilityAsync_ShouldReturnHighScore_ForWellStructuredContent()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new WikiPage
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
        Assert.NotNull(result);
        Assert.True(result.AverageReadabilityScore >= 80);
        Assert.True(result.AverageWordCount > 50);
        Assert.True(result.AverageSectionCount >= 3);
        Assert.Single(result.PageScores);
        
        var pageScore = result.PageScores.First();
        Assert.Equal("WellStructuredPage", pageScore.PageId);
        Assert.True(pageScore.Score >= 80);
        Assert.Empty(pageScore.Issues);
    }

    [Fact]
    public async Task AnalyzeReadabilityAsync_ShouldReturnLowScore_ForPoorlyStructuredContent()
    {
        // Arrange
        var pages = new List<WikiPage>
        {
            new WikiPage
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
        Assert.NotNull(result);
        Assert.True(result.AverageReadabilityScore < 70);
        Assert.True(result.AverageWordCount < 50);
        Assert.Equal(0, result.AverageSectionCount);
        
        var pageScore = result.PageScores.First();
        Assert.Equal("PoorPage", pageScore.PageId);
        Assert.True(pageScore.Score < 70);
        Assert.NotEmpty(pageScore.Issues);
        Assert.Contains(pageScore.Issues, issue => issue.Contains("too brief"));
        Assert.Contains(pageScore.Issues, issue => issue.Contains("lacks proper sections"));
    }

    [Fact]
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
        Assert.NotNull(result);
        Assert.Equal(repositoryPath, result.RepositoryPath);
        Assert.Equal(metrics, result.Metrics);
        Assert.NotNull(result.Comparisons);
        Assert.True(result.Comparisons.Count >= 4);
        Assert.NotEmpty(result.Summary);
        Assert.NotEmpty(result.ActionItems);

        // Check that comparisons include expected metrics
        var coverageComparison = result.Comparisons.FirstOrDefault(c => c.Metric == "Component Coverage");
        Assert.NotNull(coverageComparison);
        Assert.Equal(85.0, coverageComparison.CurrentValue); // Average of 90 and 80
        Assert.NotEmpty(coverageComparison.Status);
    }

    [Theory]
    [InlineData(95.0, "Average")] // 95 vs 85 benchmark = Average (61.76 percentile)
    [InlineData(85.0, "Average")] // 85 vs 85 benchmark = Average (50 percentile)  
    [InlineData(70.0, "Poor")] // 70 vs 85 benchmark = Poor (32.35 percentile)
    [InlineData(45.0, "Poor")] // 45 vs 85 benchmark = Poor (2.94 percentile)
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
        Assert.NotNull(coverageComparison);
        Assert.Equal(score, coverageComparison.CurrentValue);
        Assert.Equal(expectedStatus, coverageComparison.Status);
    }

    [Fact]
    public async Task EvaluateDocumentationQualityAsync_ShouldGenerateRecommendations_WhenQualityIsLow()
    {
        // Arrange
        var structure = new WikiStructure { Title = "Test" };
        var pages = new List<WikiPage>(); // No pages
        var components = new List<CodeComponent>
        {
            new CodeComponent { Id = "Component1", Name = "Component1", Type = "Class" }
        };

        // Act
        var result = await _service.EvaluateDocumentationQualityAsync(structure, pages, components);

        // Assert
        Assert.NotNull(result.Recommendations);
        Assert.True(result.Recommendations.Count >= 2);
        Assert.Contains(result.Recommendations, r => r.Contains("Document"));
        Assert.True(result.OverallScore < 70); // Should be low due to missing documentation
    }
}