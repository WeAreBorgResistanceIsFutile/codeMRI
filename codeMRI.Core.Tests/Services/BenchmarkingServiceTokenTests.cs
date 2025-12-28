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
}
