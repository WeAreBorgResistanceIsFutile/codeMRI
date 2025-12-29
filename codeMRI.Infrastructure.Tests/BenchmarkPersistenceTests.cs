using System;
using System.IO;
using System.Threading.Tasks;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace codeMRI.Infrastructure.Tests;

/// <summary>
/// Tests to ensure Benchmark and Server use the same database paths.
/// This prevents the issue where Benchmark writes to one DB and UI reads from another.
/// </summary>
[TestFixture]
public class BenchmarkPersistenceTests
{
    private string _testDbPath;
    private string _connectionString;

    [SetUp]
    public void Setup()
    {
        // Use a temporary database for testing
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_benchmark_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_testDbPath}";
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Test]
    public async Task BenchmarkAndServerShouldUseTheSameDatabasePath()
    {
        // Arrange: Load configurations from both projects
        var benchmarkConfig = LoadConfiguration("codeMRI.Benchmark");
        var serverConfig = LoadConfiguration("codeMRI.Server");

        // Get connection strings
        var benchmarkConnString = benchmarkConfig.GetConnectionString("BenchmarkDb");
        var serverConnString = serverConfig.GetConnectionString("BenchmarkDb");

        Assert.That(benchmarkConnString, Is.Not.Null);
        Assert.That(serverConnString, Is.Not.Null);

        // Simulate ContentRootPath behavior - both projects should use their project root
        // which is the directory containing the .csproj file
        var benchmarkProjectRoot = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "codeMRI.Benchmark"));
        var serverProjectRoot = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "codeMRI.Server"));

        // Resolve paths relative to ContentRootPath (project root)
        var benchmarkAbsolutePath = ResolveConnectionStringPath(
            benchmarkConnString, 
            benchmarkProjectRoot);
        
        var serverAbsolutePath = ResolveConnectionStringPath(
            serverConnString, 
            serverProjectRoot);

        // Assert: Both should resolve to the same absolute path
        Assert.That(benchmarkAbsolutePath, Is.EqualTo(serverAbsolutePath));
    }

    [Test]
    public async Task QualityScoresShouldPersistAcrossRepositoryInstances()
    {
        // Arrange: Create a benchmark run and add page benchmarks with quality scores
        var repository1 = new BenchmarkRepository(_connectionString);
        
        var run = new BenchmarkRun
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Run",
            RepositoryUrl = "https://github.com/test/repo",
            Status = BenchmarkStatus.Running,
            StartTime = DateTime.UtcNow
        };

        await repository1.CreateAsync(run);

        var pageBenchmark = new PageBenchmark
        {
            PageId = "page1",
            ModuleId = "module1",
            ModuleName = "Test Module",
            WordCount = 100,
            OverallQualityScore = 0.85 // This should persist
        };

        await repository1.AddPageBenchmarkAsync(run.Id, pageBenchmark);

        // Act: Create a new repository instance (simulating Server reading after Benchmark writes)
        var repository2 = new BenchmarkRepository(_connectionString);
        var retrievedBenchmarks = await repository2.GetPageBenchmarksAsync(run.Id);

        // Assert: Quality score should be persisted
        Assert.That(retrievedBenchmarks, Has.Count.EqualTo(1));
        Assert.That(retrievedBenchmarks[0].OverallQualityScore, Is.EqualTo(0.85));
        Assert.That(retrievedBenchmarks[0].PageId, Is.EqualTo("page1"));
        Assert.That(retrievedBenchmarks[0].ModuleId, Is.EqualTo("module1"));
    }

    [Test]
    public async Task UpdatedQualityScoresShouldOverwritePreviousValues()
    {
        // Arrange: Create initial page benchmark with placeholder score
        var repository = new BenchmarkRepository(_connectionString);
        
        var run = new BenchmarkRun
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Run",
            RepositoryUrl = "https://github.com/test/repo",
            Status = BenchmarkStatus.Running,
            StartTime = DateTime.UtcNow
        };

        await repository.CreateAsync(run);

        var pageBenchmark = new PageBenchmark
        {
            PageId = "page1",
            ModuleId = "module1",
            ModuleName = "Test Module",
            WordCount = 100,
            OverallQualityScore = -1.0 // Placeholder
        };

        await repository.AddPageBenchmarkAsync(run.Id, pageBenchmark);

        // Act: Update with actual quality score (simulating Judge evaluation)
        pageBenchmark.OverallQualityScore = 0.75;
        await repository.AddPageBenchmarkAsync(run.Id, pageBenchmark);

        // Assert: Score should be updated
        var retrievedBenchmarks = await repository.GetPageBenchmarksAsync(run.Id);
        Assert.That(retrievedBenchmarks, Has.Count.EqualTo(1));
        Assert.That(retrievedBenchmarks[0].OverallQualityScore, Is.EqualTo(0.75));
    }

    private IConfiguration LoadConfiguration(string projectName)
    {
        var projectPath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "..", "..", 
            projectName);

        var configPath = Path.Combine(projectPath, "appsettings.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var configData = new Dictionary<string, string>();
        var json = File.ReadAllText(configPath);
        
        // Simple JSON parsing for connection strings
        var connStringStart = json.IndexOf("\"ConnectionStrings\"");
        if (connStringStart != -1)
        {
            var benchmarkDbStart = json.IndexOf("\"BenchmarkDb\"", connStringStart);
            if (benchmarkDbStart != -1)
            {
                var valueStart = json.IndexOf(":", benchmarkDbStart) + 1;
                var valueEnd = json.IndexOf(",", valueStart);
                if (valueEnd == -1) valueEnd = json.IndexOf("\n", valueStart);
                if (valueEnd == -1) valueEnd = json.IndexOf("}", valueStart);
                
                var value = json.Substring(valueStart, valueEnd - valueStart)
                    .Trim()
                    .Trim('"', ' ', '\r', '\n', '\t', ',', '}');
                    
                configData["ConnectionStrings:BenchmarkDb"] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }

    private string ResolveConnectionStringPath(string connectionString, string basePath)
    {
        // Extract "Data Source=..." from connection string
        var dataSourcePrefix = "Data Source=";
        var startIndex = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);
        
        if (startIndex == -1)
        {
            throw new ArgumentException("Connection string does not contain 'Data Source='");
        }

        var relativePath = connectionString.Substring(startIndex + dataSourcePrefix.Length)
            .Split(';')[0]
            .Trim();

        // If path is already absolute, return it as-is
        if (Path.IsPathRooted(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        // Resolve relative path to absolute
        var absolutePath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        return absolutePath;
    }
}
