using NUnit.Framework;
using Moq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class BenchmarkingServiceTests
{
    private Mock<IBenchmarkRepository> _mockRepository;
    private Mock<ILogger<BenchmarkingService>> _mockLogger;
    private BenchmarkingService _service;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<IBenchmarkRepository>();
        _mockLogger = new Mock<ILogger<BenchmarkingService>>();
        _service = new BenchmarkingService(_mockRepository.Object, _mockLogger.Object);
    }

    #region StartBenchmarkRunAsync Tests

    [Test]
    public async Task StartBenchmarkRunAsync_ShouldCreateNewBenchmarkRun()
    {
        // Arrange
        var repositoryUrl = "https://github.com/test/repo";
        var name = "Test Benchmark";
        var modelConfigJson = "{\"DocumentationModel\": \"test-model\"}";

        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<BenchmarkRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BenchmarkRun run, CancellationToken ct) => run);

        // Act
        var result = await _service.StartBenchmarkRunAsync(repositoryUrl, name, modelConfigJson);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RepositoryUrl, Is.EqualTo(repositoryUrl));
        Assert.That(result.Name, Is.EqualTo(name));
        Assert.That(result.ModelConfiguration, Is.EqualTo(modelConfigJson));
        Assert.That(result.Status, Is.EqualTo(BenchmarkStatus.Running));
    }

    [Test]
    public async Task StartBenchmarkRunAsync_ShouldSetStartTime()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<BenchmarkRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BenchmarkRun run, CancellationToken ct) => run);

        // Act
        var result = await _service.StartBenchmarkRunAsync("url", "name", "{}");

        // Assert
        Assert.That(result.StartTime, Is.GreaterThanOrEqualTo(before));
        Assert.That(result.EndTime, Is.Null);
    }

    #endregion

    #region RecordMetrics Tests

    [Test]
    public void RecordMetrics_ShouldAccumulateTokenCounts()
    {
        // Arrange
        var runId = "test-run-id";
        var metrics1 = new BenchmarkMetrics { InputTokens = 100, OutputTokens = 50 };
        var metrics2 = new BenchmarkMetrics { InputTokens = 200, OutputTokens = 100 };

        // Act
        _service.RecordMetrics(runId, metrics1);
        _service.RecordMetrics(runId, metrics2);

        // Assert - Verify metrics are being stored
        _mockRepository.Verify(r => r.AddMetricsAsync(runId, metrics1, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.AddMetricsAsync(runId, metrics2, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RecordPageBenchmarkAsync Tests

    [Test]
    public async Task RecordPageBenchmarkAsync_ShouldStorePageBenchmark()
    {
        // Arrange
        var runId = "test-run-id";
        var pageBenchmark = new PageBenchmark
        {
            PageId = "page-1",
            ModuleId = "module-1",
            OverallQualityScore = 0.85
        };

        // Act
        await _service.RecordPageBenchmarkAsync(runId, pageBenchmark);

        // Assert
        _mockRepository.Verify(r => r.AddPageBenchmarkAsync(runId, pageBenchmark, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdatePhaseAsync Tests

    [Test]
    public async Task UpdatePhaseAsync_ShouldUpdateRunPhase()
    {
        // Arrange
        var runId = "test-run-id";
        var existingRun = new BenchmarkRun { Id = runId, CurrentPhase = "Cloning" };
        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);

        // Act
        await _service.UpdatePhaseAsync(runId, "Generating", 50);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BenchmarkRun>(run => run.CurrentPhase == "Generating" && run.ProgressPercentage == 50),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CompleteBenchmarkRunAsync Tests

    [Test]
    public async Task CompleteBenchmarkRunAsync_ShouldSetEndTimeAndStatus()
    {
        // Arrange
        var runId = "test-run-id";
        var existingRun = new BenchmarkRun
        {
            Id = runId,
            Status = BenchmarkStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-30)
        };
        var pageMetrics = new List<PageBenchmark>
        {
            new() { OverallQualityScore = 0.8 },
            new() { OverallQualityScore = 0.9 }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);
        _mockRepository
            .Setup(r => r.GetPageBenchmarksAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageMetrics);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<BenchmarkRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CompleteBenchmarkRunAsync(runId);

        // Assert
        Assert.That(result.Status, Is.EqualTo(BenchmarkStatus.Completed));
        Assert.That(result.EndTime, Is.Not.Null);
        Assert.That(result.MeanQualityScore, Is.EqualTo(0.85).Within(0.01));
    }

    #endregion

    #region PauseBenchmarkRunAsync Tests

    [Test]
    public async Task PauseBenchmarkRunAsync_ShouldSetStatusToPaused()
    {
        // Arrange
        var runId = "test-run-id";
        var existingRun = new BenchmarkRun { Id = runId, Status = BenchmarkStatus.Running };
        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);

        // Act
        await _service.PauseBenchmarkRunAsync(runId);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<BenchmarkRun>(run => run.Status == BenchmarkStatus.Paused),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ResumeBenchmarkRunAsync Tests

    [Test]
    public async Task ResumeBenchmarkRunAsync_ShouldSetStatusToRunning()
    {
        // Arrange
        var runId = "test-run-id";
        var existingRun = new BenchmarkRun
        {
            Id = runId,
            Status = BenchmarkStatus.Paused,
            ProcessedModules = new HashSet<string> { "module-1" }
        };
        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);

        // Act
        var result = await _service.ResumeBenchmarkRunAsync(runId);

        // Assert
        Assert.That(result.Status, Is.EqualTo(BenchmarkStatus.Running));
        Assert.That(result.ProcessedModules, Contains.Item("module-1"));
    }

    #endregion

    #region GenerateReportAsync Tests

    [Test]
    public async Task GenerateReportAsync_ShouldReturnMarkdownReport()
    {
        // Arrange
        var runId = "test-run-id";
        var run = new BenchmarkRun
        {
            Id = runId,
            Name = "Test Run",
            RepositoryName = "test-repo",
            MeanQualityScore = 0.85,
            TotalInputTokens = 100000,
            TotalOutputTokens = 50000,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow
        };
        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _mockRepository
            .Setup(r => r.GetPageBenchmarksAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageBenchmark>());

        // Act
        var report = await _service.GenerateReportAsync(runId);

        // Assert
        Assert.That(report, Does.Contain("# Benchmark Report"));
        Assert.That(report, Does.Contain("test-repo"));
        Assert.That(report, Does.Contain("0.85"));
    }

    #endregion

    #region CompareRunsAsync Tests

    [Test]
    public async Task CompareRunsAsync_ShouldIdentifyFastestAndHighestQuality()
    {
        // Arrange
        var run1 = new BenchmarkRun
        {
            Id = "run-1",
            Name = "Fast Run",
            MeanQualityScore = 0.80,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow
        };
        var run2 = new BenchmarkRun
        {
            Id = "run-2",
            Name = "Quality Run",
            MeanQualityScore = 0.95,
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            EndTime = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run1);
        _mockRepository
            .Setup(r => r.GetByIdAsync("run-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run2);
        _mockRepository
            .Setup(r => r.SaveComparisonAsync(It.IsAny<ModelBenchmarkComparison>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelBenchmarkComparison c, CancellationToken _) => c);

        // Act
        var comparison = await _service.CompareRunsAsync(new List<string> { "run-1", "run-2" });

        // Assert
        Assert.That(comparison.FastestConfiguration, Is.EqualTo("Fast Run"));
        Assert.That(comparison.HighestQualityConfiguration, Is.EqualTo("Quality Run"));
    }

    #endregion
}
