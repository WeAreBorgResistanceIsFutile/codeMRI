using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Diagnostics;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class DbIngestionManagerTests
{
    private Mock<ILogger<DbIngestionManager>> _mockLogger = null!;
    private Mock<IDebugSnapshotService> _mockSnapshotService = null!;
    private Mock<ICodeWikiOrchestrator> _mockOrchestrator = null!;
    private Mock<IWikiRepository> _mockWikiRepo = null!;
    private AgentMessageBus _messageBus = null!;
    private Mock<IServiceScopeFactory> _mockScopeFactory = null!;
    private Mock<IServiceProvider> _mockServiceProvider = null!;
    private Mock<IServiceScope> _mockScope = null!;
    private DbIngestionManager _manager = null!;
    private string _dbPath = null!;
    private string _tempRepoPath = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<DbIngestionManager>>();
        _mockSnapshotService = new Mock<IDebugSnapshotService>();
        _mockOrchestrator = new Mock<ICodeWikiOrchestrator>();
        _mockWikiRepo = new Mock<IWikiRepository>();
        _messageBus = new AgentMessageBus(new Mock<ILogger<AgentMessageBus>>().Object);
        
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScope = new Mock<IServiceScope>();

        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ICodeWikiOrchestrator))).Returns(_mockOrchestrator.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IWikiRepository))).Returns(_mockWikiRepo.Object);

        _dbPath = Path.Combine(Path.GetTempPath(), $"ingestion_test_{Guid.NewGuid()}.db");
        _manager = new DbIngestionManager(_mockLogger.Object, _messageBus, _mockScopeFactory.Object, _mockSnapshotService.Object, _dbPath);
        
        _tempRepoPath = Path.Combine(Path.GetTempPath(), "codeMRI_ingestion_repo_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRepoPath);
        
        // Initialize as git repo to satisfy GitHelper
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = _tempRepoPath,
            RedirectStandardOutput = true,
            UseShellExecute = false
        });
        process?.WaitForExit();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_tempRepoPath)) Directory.Delete(_tempRepoPath, true);
    }

    [Test]
    public async Task RunJobAsync_WhenCompleted_ShouldCallDeleteSnapshotsAsync()
    {
        // Arrange
        var localRepoPath = _tempRepoPath;
        
        _mockOrchestrator.Setup(x => x.GenerateAdvancedWikiAsync(
            It.IsAny<string>(), 
            It.IsAny<RepositoryInfo>(), 
            It.IsAny<IProgress<ProgressInfo>>(), 
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WikiStructure());

        var job = await _manager.StartJobAsync(localRepoPath, true, AudienceType.Developer);
        
        // Wait for job to complete
        var timeout = TimeSpan.FromSeconds(10);
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            var currentJob = await _manager.GetJobAsync(job.Id);
            if (currentJob?.Status == IngestionStatus.Completed || currentJob?.Status == IngestionStatus.Failed)
                break;
            await Task.Delay(100);
        }

        // Assert
        _mockSnapshotService.Verify(x => x.DeleteSnapshotsAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Reingestion_SamePath_ShouldNotDeleteAndFail()
    {
        // This test reproduces the bug where providing the internal repo path as the source URL
        // causes the manager to delete the directory (target) which is also the source, causing git clone to fail.
        
        // Arrange
        var repoName = "ReingestionTestRepo";
        // Mirrors the logic in DbIngestionManager
        var targetDir = Path.GetFullPath(Path.Combine("../data/repos", repoName));
        
        // Ensure we clean up before and after
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);

        // Initialize a dummy git repo there
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = targetDir,
            RedirectStandardOutput = true,
            UseShellExecute = false
        });
        await process!.WaitForExitAsync();
        
        // Create a dummy file to commit so it's a valid repo
        File.WriteAllText(Path.Combine(targetDir, "README.md"), "# Test");
        Process.Start(new ProcessStartInfo { FileName = "git", Arguments = "add .", WorkingDirectory = targetDir })!.WaitForExit();
        Process.Start(new ProcessStartInfo { FileName = "git", Arguments = "commit -m 'Initial'", WorkingDirectory = targetDir })!.WaitForExit();

        try 
        {
            // Act
            // We pass the targetDir itself as the repoUrl
            var job = await _manager.StartJobAsync(targetDir, true, AudienceType.Developer);

            // Wait for job to complete or fail
            var timeout = TimeSpan.FromSeconds(10);
            var start = DateTime.UtcNow;
            IngestionJob? finalJob = null;
            while (DateTime.UtcNow - start < timeout)
            {
                finalJob = await _manager.GetJobAsync(job.Id);
                if (finalJob?.Status == IngestionStatus.Completed || finalJob?.Status == IngestionStatus.Failed)
                    break;
                await Task.Delay(100);
            }

            // Assert
            // FIXED BEHAVIOR: It should succeed because we skipped deletion
            if (finalJob?.Status == IngestionStatus.Failed)
            {
                Assert.Fail($"Job failed unexpectedly with error: {finalJob.Error}");
            }
            Assert.That(finalJob?.Status, Is.EqualTo(IngestionStatus.Completed), "Job should complete successfully when re-ingesting existing repo");
        }
        finally
        {
            // Cleanup provided it still exists or if it was deleted (handled by EnsureDeleted)
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        }
    }
}
