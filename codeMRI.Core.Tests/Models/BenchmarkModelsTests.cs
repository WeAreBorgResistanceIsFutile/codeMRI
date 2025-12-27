using NUnit.Framework;
using codeMRI.Core.Models;

namespace codeMRI.Core.Tests.Models;

[TestFixture]
public class BenchmarkModelsTests
{
    #region BenchmarkMetrics Tests

    [Test]
    public void BenchmarkMetrics_TotalTokens_ShouldSumInputAndOutput()
    {
        // Arrange
        var metrics = new BenchmarkMetrics
        {
            InputTokens = 1000,
            OutputTokens = 500
        };

        // Act
        var total = metrics.TotalTokens;

        // Assert
        Assert.That(total, Is.EqualTo(1500));
    }

    [Test]
    public void BenchmarkMetrics_TokensPerSecond_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new BenchmarkMetrics
        {
            OutputTokens = 100,
            Latency = TimeSpan.FromSeconds(2)
        };

        // Act
        var tokensPerSecond = metrics.TokensPerSecond;

        // Assert
        Assert.That(tokensPerSecond, Is.EqualTo(50.0).Within(0.1));
    }

    [Test]
    public void BenchmarkMetrics_TokensPerSecond_ShouldReturnZeroWhenLatencyIsZero()
    {
        // Arrange
        var metrics = new BenchmarkMetrics
        {
            OutputTokens = 100,
            Latency = TimeSpan.Zero
        };

        // Act
        var tokensPerSecond = metrics.TokensPerSecond;

        // Assert
        Assert.That(tokensPerSecond, Is.EqualTo(0));
    }

    [Test]
    public void BenchmarkMetrics_ShouldGenerateUniqueIds()
    {
        // Arrange & Act
        var metrics1 = new BenchmarkMetrics();
        var metrics2 = new BenchmarkMetrics();

        // Assert
        Assert.That(metrics1.Id, Is.Not.EqualTo(metrics2.Id));
    }

    #endregion

    #region BenchmarkRun Tests

    [Test]
    public void BenchmarkRun_TotalDuration_ShouldCalculateFromStartToEnd()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            StartTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2025, 1, 1, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var duration = run.TotalDuration;

        // Assert
        Assert.That(duration, Is.EqualTo(TimeSpan.FromMinutes(30)));
    }

    [Test]
    public void BenchmarkRun_TotalDuration_ShouldUseCurrentTimeWhenNotEnded()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = null
        };

        // Act
        var duration = run.TotalDuration;

        // Assert - Duration should be approximately 5 minutes (with some tolerance)
        Assert.That(duration.TotalMinutes, Is.GreaterThanOrEqualTo(4.9).And.LessThanOrEqualTo(5.1));
    }

    [Test]
    public void BenchmarkRun_TotalTokens_ShouldSumInputAndOutput()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            TotalInputTokens = 100000,
            TotalOutputTokens = 50000
        };

        // Act
        var total = run.TotalTokens;

        // Assert
        Assert.That(total, Is.EqualTo(150000));
    }

    [Test]
    public void BenchmarkRun_DefaultStatus_ShouldBeRunning()
    {
        // Arrange & Act
        var run = new BenchmarkRun();

        // Assert
        Assert.That(run.Status, Is.EqualTo(BenchmarkStatus.Running));
    }

    [Test]
    public void BenchmarkRun_ProcessedModules_ShouldSupportAddingModules()
    {
        // Arrange
        var run = new BenchmarkRun();

        // Act
        run.ProcessedModules.Add("module-1");
        run.ProcessedModules.Add("module-2");

        // Assert
        Assert.That(run.ProcessedModules, Has.Count.EqualTo(2));
        Assert.That(run.ProcessedModules, Does.Contain("module-1"));
    }

    #endregion

    #region PageBenchmark Tests

    [Test]
    public void PageBenchmark_ShouldInitializeWithEmptyCollections()
    {
        // Arrange & Act
        var pageBenchmark = new PageBenchmark();

        // Assert
        Assert.That(pageBenchmark.CategoryScores, Is.Not.Null);
        Assert.That(pageBenchmark.CategoryScores, Is.Empty);
        Assert.That(pageBenchmark.RequirementAssessments, Is.Not.Null);
        Assert.That(pageBenchmark.RequirementAssessments, Is.Empty);
    }

    [Test]
    public void PageBenchmark_ShouldStoreQualityMetrics()
    {
        // Arrange
        var pageBenchmark = new PageBenchmark
        {
            OverallQualityScore = 0.85,
            JudgeConsensusLevel = 0.92
        };
        pageBenchmark.CategoryScores["Completeness"] = 0.88;
        pageBenchmark.CategoryScores["Clarity"] = 0.82;

        // Assert
        Assert.That(pageBenchmark.OverallQualityScore, Is.EqualTo(0.85));
        Assert.That(pageBenchmark.JudgeConsensusLevel, Is.EqualTo(0.92));
        Assert.That(pageBenchmark.CategoryScores, Has.Count.EqualTo(2));
    }

    #endregion

    #region ModelBenchmarkComparison Tests

    [Test]
    public void ModelBenchmarkComparison_ShouldInitializeWithEmptyCollections()
    {
        // Arrange & Act
        var comparison = new ModelBenchmarkComparison();

        // Assert
        Assert.That(comparison.Runs, Is.Not.Null);
        Assert.That(comparison.Runs, Is.Empty);
        Assert.That(comparison.QualityRankings, Is.Not.Null);
        Assert.That(comparison.SpeedRankings, Is.Not.Null);
        Assert.That(comparison.EfficiencyRankings, Is.Not.Null);
    }

    [Test]
    public void ModelBenchmarkComparison_ShouldSetGeneratedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var comparison = new ModelBenchmarkComparison();

        // Assert
        Assert.That(comparison.GeneratedAt, Is.GreaterThanOrEqualTo(before));
        Assert.That(comparison.GeneratedAt, Is.LessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1)));
    }

    #endregion

    #region AggregateMetrics Tests

    [Test]
    public void AggregateMetrics_ShouldStoreAllMetricTypes()
    {
        // Arrange
        var metrics = new AggregateMetrics
        {
            Count = 100,
            TotalTime = TimeSpan.FromMinutes(30),
            MeanTime = TimeSpan.FromSeconds(18),
            MedianTime = TimeSpan.FromSeconds(15),
            P95Time = TimeSpan.FromSeconds(45),
            TotalTokens = 500000,
            MeanTokensPerCall = 5000,
            SuccessRate = 0.98,
            TotalRetries = 5
        };

        // Assert
        Assert.That(metrics.Count, Is.EqualTo(100));
        Assert.That(metrics.TotalTime, Is.EqualTo(TimeSpan.FromMinutes(30)));
        Assert.That(metrics.SuccessRate, Is.EqualTo(0.98));
        Assert.That(metrics.TotalRetries, Is.EqualTo(5));
    }

    #endregion

    #region BenchmarkStatus Tests

    [Test]
    public void BenchmarkStatus_ShouldHaveAllExpectedValues()
    {
        // Assert all expected statuses exist
        Assert.That((int)BenchmarkStatus.Running, Is.EqualTo(0));
        Assert.That((int)BenchmarkStatus.Paused, Is.EqualTo(1));
        Assert.That((int)BenchmarkStatus.Completed, Is.EqualTo(2));
        Assert.That((int)BenchmarkStatus.Failed, Is.EqualTo(3));
        Assert.That((int)BenchmarkStatus.Cancelled, Is.EqualTo(4));
    }

    #endregion
}
