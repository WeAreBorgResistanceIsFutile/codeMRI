using NUnit.Framework;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Data.Sqlite;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class BenchmarkRepositoryPageBenchmarkTests
{
    private BenchmarkRepository _repository;
    private string _connectionString;
    private string _dbPath;

    [SetUp]
    public void SetUp()
    {
        // Use a temporary file-based database for tests
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_benchmark_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";
        _repository = new BenchmarkRepository(_connectionString);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up the test database
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Test]
    public async Task AddPageBenchmarkAsync_WhenCalledTwiceWithSamePageId_ShouldUpdateNotDuplicate()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            Id = "test-run",
            Name = "Test Run",
            RepositoryUrl = "https://github.com/test/repo",
            Status = BenchmarkStatus.Running
        };
        await _repository.CreateAsync(run);

        var pageBenchmark1 = new PageBenchmark
        {
            PageId = "page-1",
            ModuleId = "module-1",
            ModuleName = "Test Module",
            OverallQualityScore = -1.0, // Placeholder
            WordCount = 100
        };

        var pageBenchmark2 = new PageBenchmark
        {
            PageId = "page-1", // Same PageId
            ModuleId = "module-1",
            ModuleName = "Test Module",
            OverallQualityScore = 0.85, // Updated score from Judge
            WordCount = 100
        };

        // Act
        await _repository.AddPageBenchmarkAsync(run.Id, pageBenchmark1);
        await _repository.AddPageBenchmarkAsync(run.Id, pageBenchmark2); // Should update, not insert

        // Assert
        var pageBenchmarks = await _repository.GetPageBenchmarksAsync(run.Id);
        Assert.That(pageBenchmarks, Has.Count.EqualTo(1), "Should have only 1 page benchmark, not 2");
        Assert.That(pageBenchmarks[0].OverallQualityScore, Is.EqualTo(0.85), "Should have the updated quality score");
    }

    [Test]
    public async Task AddPageBenchmarkAsync_WithDifferentPageIds_ShouldInsertBoth()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            Id = "test-run",
            Name = "Test Run",
            RepositoryUrl = "https://github.com/test/repo",
            Status = BenchmarkStatus.Running
        };
        await _repository.CreateAsync(run);

        var pageBenchmark1 = new PageBenchmark
        {
            PageId = "page-1",
            ModuleId = "module-1",
            ModuleName = "Module 1",
            OverallQualityScore = 0.8,
            WordCount = 100
        };

        var pageBenchmark2 = new PageBenchmark
        {
            PageId = "page-2", // Different PageId
            ModuleId = "module-2",
            ModuleName = "Module 2",
            OverallQualityScore = 0.9,
            WordCount = 200
        };

        // Act
        await _repository.AddPageBenchmarkAsync(run.Id, pageBenchmark1);
        await _repository.AddPageBenchmarkAsync(run.Id, pageBenchmark2);

        // Assert
        var pageBenchmarks = await _repository.GetPageBenchmarksAsync(run.Id);
        Assert.That(pageBenchmarks, Has.Count.EqualTo(2), "Should have 2 different page benchmarks");
    }

    [Test]
    public async Task AddPageBenchmarkAsync_UpdatePreservesOtherFields()
    {
        // Arrange
        var run = new BenchmarkRun
        {
            Id = "test-run",
            Name = "Test Run",
            RepositoryUrl = "https://github.com/test/repo",
            Status = BenchmarkStatus.Running
        };
        await _repository.CreateAsync(run);

        var pageBenchmark1 = new PageBenchmark
        {
            PageId = "page-1",
            ModuleId = "module-1",
            ModuleName = "Test Module",
            OverallQualityScore = -1.0,
            WordCount = 100,
            SectionCount = 5,
            CodeBlockCount = 3
        };

        var pageBenchmark2 = new PageBenchmark
        {
            PageId = "page-1",
            ModuleId = "module-1",
            ModuleName = "Test Module",
            OverallQualityScore = 0.85, // Only updating quality score
            WordCount = 100,
            SectionCount = 5,
            CodeBlockCount = 3
        };

        // Act
        await _repository.AddPageBenchmarkAsync(run.Id, pageBenchmark1);
        await _repository.AddPageBenchmarkAsync(run.Id, pageBenchmark2);

        // Assert
        var pageBenchmarks = await _repository.GetPageBenchmarksAsync(run.Id);
        Assert.That(pageBenchmarks, Has.Count.EqualTo(1));
        Assert.That(pageBenchmarks[0].WordCount, Is.EqualTo(100));
        Assert.That(pageBenchmarks[0].SectionCount, Is.EqualTo(5));
        Assert.That(pageBenchmarks[0].CodeBlockCount, Is.EqualTo(3));
        Assert.That(pageBenchmarks[0].OverallQualityScore, Is.EqualTo(0.85));
    }
}
