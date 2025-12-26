using System;
using System.IO;
using System.Threading.Tasks;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Infrastructure.Tests.Services
{
    [TestFixture]
    public class DbGenerationJobManagerTests
    {
        private string _dbPath;
        private Mock<ILogger<DbGenerationJobManager>> _mockLogger;
        private AgentMessageBus _messageBus;
        private Mock<IServiceScopeFactory> _mockScopeFactory;
        private Mock<IServiceScope> _mockScope;
        private Mock<IServiceProvider> _mockServiceProvider;
        private Mock<ICodeWikiOrchestrator> _mockOrchestrator;
        private Mock<IWikiRepository> _mockWikiRepo;

        [SetUp]
        public void Setup()
        {
            _dbPath = Path.GetTempFileName();
            _mockLogger = new Mock<ILogger<DbGenerationJobManager>>();
            _messageBus = new AgentMessageBus(new Mock<ILogger<AgentMessageBus>>().Object);
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
            _mockScope = new Mock<IServiceScope>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockOrchestrator = new Mock<ICodeWikiOrchestrator>();
            _mockWikiRepo = new Mock<IWikiRepository>();

            _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
            _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(ICodeWikiOrchestrator))).Returns(_mockOrchestrator.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(IWikiRepository))).Returns(_mockWikiRepo.Object);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Test]
        public async Task StartJobAsync_ShouldCreateNewJob_WhenNoActiveJobExists()
        {
            // Arrange
            var manager = new DbGenerationJobManager(_mockLogger.Object, _messageBus, _mockScopeFactory.Object, _dbPath);
            var request = new StructureRequest { RepoPath = "/test/repo", Language = "C#" };

            // Act
            var job = await manager.StartJobAsync("/test/repo", request);

            // Assert
            Assert.That(job, Is.Not.Null);
            Assert.That(job.RepoPath, Is.EqualTo("/test/repo"));
            Assert.That(job.Status, Is.EqualTo(GenerationStatus.Queued));
            Assert.That(job.Id, Is.Not.Null);

            // Verify DB
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            var savedJob = await connection.QueryFirstOrDefaultAsync<GenerationJob>("SELECT * FROM GenerationJobs WHERE Id = @Id", new { job.Id });
            Assert.That(savedJob, Is.Not.Null);
        }

        [Test]
        public async Task GetJobAsync_ShouldReturnJob_WhenJobExists()
        {
            // Arrange
            var manager = new DbGenerationJobManager(_mockLogger.Object, _messageBus, _mockScopeFactory.Object, _dbPath);
            var request = new StructureRequest { RepoPath = "/test/repo" };
            var job = await manager.StartJobAsync("/test/repo", request);

            // Act
            var retrievedJob = await manager.GetJobAsync(job.Id);

            // Assert
            Assert.That(retrievedJob, Is.Not.Null);
            Assert.That(retrievedJob.Id, Is.EqualTo(job.Id));
            Assert.That(retrievedJob.RepoPath, Is.EqualTo(job.RepoPath));
        }
    }
}
