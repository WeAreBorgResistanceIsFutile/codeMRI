using NUnit.Framework;
using Moq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class BenchmarkingServiceTokenTests
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

    [Test]
    public async Task CompleteBenchmarkRunAsync_ShouldCalculateTotalTokensFromRepository_WhenBufferIsEmpty()
    {
        // Arrange
        var runId = "test-run-id";
        var existingRun = new BenchmarkRun
        {
            Id = runId,
            Status = BenchmarkStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            TotalInputTokens = 0,
            TotalOutputTokens = 0
        };

        var repoMetrics = new List<BenchmarkMetrics>
        {
            new() { InputTokens = 100, OutputTokens = 50 },
            new() { InputTokens = 200, OutputTokens = 100 }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);
        
        // Mock getting page benchmarks (required by method)
        _mockRepository
            .Setup(r => r.GetPageBenchmarksAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageBenchmark>());

        // Mock getting metrics from repository - this is what we expect the service to call
        _mockRepository
            .Setup(r => r.GetMetricsAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoMetrics);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<BenchmarkRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        // We do NOT call RecordMetrics, so the internal _metricsBuffer is empty.
        // This simulates a service restart where the buffer is lost but DB has data.
        var result = await _service.CompleteBenchmarkRunAsync(runId);

        // Assert
        // Expected total: (100+200) input, (50+100) output
        Assert.That(result.TotalInputTokens, Is.EqualTo(300), "TotalInputTokens should be calculated from repository metrics");
        Assert.That(result.TotalOutputTokens, Is.EqualTo(150), "TotalOutputTokens should be calculated from repository metrics");
    }
    [Test]
    public async Task CompleteBenchmarkRunAsync_ShouldCalculateTotalTokensFromBothRepositoryAndBuffer()
    {
        // Arrange
        var runId = "test-run-id";
        var existingRun = new BenchmarkRun
        {
            Id = runId,
            Status = BenchmarkStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-30)
        };

        var repoMetrics = new List<BenchmarkMetrics>
        {
            new() { Id = "m1", InputTokens = 100, OutputTokens = 50 }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);
        
        _mockRepository
            .Setup(r => r.GetPageBenchmarksAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageBenchmark>());

        _mockRepository
            .Setup(r => r.GetMetricsAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoMetrics);

        // Act
        // Record a newer metric in the buffer (same ID, or different ID)
        _service.RecordMetrics(runId, new BenchmarkMetrics { Id = "m1", InputTokens = 110, OutputTokens = 55 });
        _service.RecordMetrics(runId, new BenchmarkMetrics { Id = "m2", InputTokens = 200, OutputTokens = 100 });

        var result = await _service.CompleteBenchmarkRunAsync(runId);

        // Assert
        // Expected total: 110 (from buffer m1) + 200 (from buffer m2) = 310 input
        // Expected total: 55 (from buffer m1) + 100 (from buffer m2) = 155 output
        Assert.That(result.TotalInputTokens, Is.EqualTo(310), "TotalInputTokens should prefer buffer overlay");
        Assert.That(result.TotalOutputTokens, Is.EqualTo(155), "TotalOutputTokens should prefer buffer overlay");
    }
}
