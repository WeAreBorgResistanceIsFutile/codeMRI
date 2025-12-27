using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;

namespace codeMRI.Infrastructure.Tests;

[TestFixture]
public class ManagementTests
{
    private string _dbPath = null!;
    private string _repoPath = null!;
    private SqliteWikiRepository _wikiRepo = null!;
    private DbIngestionManager _ingestionManager = null!;
    private DbGenerationJobManager _generationManager = null!;

    [SetUp]
    public void SetUp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codeMRI_management_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        _dbPath = Path.Combine(tempDir, "test.db");
        _repoPath = Path.Combine(tempDir, "test_repo");
        Directory.CreateDirectory(_repoPath);

        _wikiRepo = new SqliteWikiRepository($"Data Source={_dbPath}");
        
        var mockIngestionLogger = new Mock<ILogger<DbIngestionManager>>();
        var mockGenerationLogger = new Mock<ILogger<DbGenerationJobManager>>();
        var mockSnapshotService = new Mock<IDebugSnapshotService>();
        var messageBus = new AgentMessageBus(new Mock<ILogger<AgentMessageBus>>().Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        _ingestionManager = new DbIngestionManager(mockIngestionLogger.Object, messageBus, mockScopeFactory.Object, mockSnapshotService.Object, _dbPath);
        _generationManager = new DbGenerationJobManager(mockGenerationLogger.Object, messageBus, mockScopeFactory.Object, _dbPath);
    }

    [TearDown]
    public void TearDown()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Test]
    public async Task IngestionJobs_ListAll_And_Delete_ShouldWork()
    {
        // Start a job
        var job = await _ingestionManager.StartJobAsync("https://github.com/test/repo", true, AudienceType.Developer);
        
        // List all
        var allJobs = await _ingestionManager.ListAllJobsAsync();
        Assert.That(allJobs.Count, Is.EqualTo(1));
        Assert.That(allJobs[0].Id, Is.EqualTo(job.Id));

        // Delete
        await _ingestionManager.DeleteJobAsync(job.Id);
        
        // Verify deleted
        allJobs = await _ingestionManager.ListAllJobsAsync();
        Assert.That(allJobs.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GenerationJobs_ListAll_And_Delete_ShouldWork()
    {
        // Start a job
        var job = await _generationManager.StartJobAsync(_repoPath, new StructureRequest());
        
        // List all
        var allJobs = await _generationManager.ListAllJobsAsync();
        Assert.That(allJobs.Count, Is.EqualTo(1));
        Assert.That(allJobs[0].Id, Is.EqualTo(job.Id));

        // Delete
        await _generationManager.DeleteJobAsync(job.Id);
        
        // Verify deleted
        allJobs = await _generationManager.ListAllJobsAsync();
        Assert.That(allJobs.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Repository_Delete_ShouldRemoveFromDbAndDisk()
    {
        // Setup repo in DB
        await _wikiRepo.SaveStructureAsync(_repoPath, new WikiStructure { Title = "Test Repo" });
        
        // Verify exists in DB
        var summaries = await _wikiRepo.GetAllRepositorySummariesAsync();
        Assert.That(summaries.Exists(s => s.Path == _repoPath), Is.True);
        
        // Verify exists on disk
        Assert.That(Directory.Exists(_repoPath), Is.True);

        // Delete
        await _wikiRepo.DeleteRepositoryAsync(_repoPath);
        
        // Verify removed from DB
        summaries = await _wikiRepo.GetAllRepositorySummariesAsync();
        Assert.That(summaries.Exists(s => s.Path == _repoPath), Is.False);
        
        // Verify removed from disk
        Assert.That(Directory.Exists(_repoPath), Is.False);
    }
}
