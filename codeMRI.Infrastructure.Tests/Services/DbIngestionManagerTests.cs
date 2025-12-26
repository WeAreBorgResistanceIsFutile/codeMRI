using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

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
}
