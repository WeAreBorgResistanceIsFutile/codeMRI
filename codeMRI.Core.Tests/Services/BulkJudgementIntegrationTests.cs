using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using codeMRI.Agents.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class BulkJudgementIntegrationTests
{
    private string _testDbPath;
    private Mock<ILogger<DbGenerationJobManager>> _mockJobManagerLogger;
    private Mock<IServiceScopeFactory> _mockScopeFactory;
    private AgentMessageBus _messageBus;
    private DbGenerationJobManager _jobManager;

    [SetUp]
    public void SetUp()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"bulk_judge_test_{Guid.NewGuid()}.db");
        _mockJobManagerLogger = new Mock<ILogger<DbGenerationJobManager>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _messageBus = new AgentMessageBus(new Mock<ILogger<AgentMessageBus>>().Object);
        _jobManager = new DbGenerationJobManager(_mockJobManagerLogger.Object, _messageBus, _mockScopeFactory.Object, _testDbPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { /* ignore */ }
        }
    }

    [Test]
    public async Task RunJudgementOnAllJobs_ShouldProcessCompletedJobsWithResults()
    {
        // 1. Arrange: Insert a completed job with results
        var wikiStructure = new WikiStructure
        {
            Title = "Test Structure",
            RepoPath = "/test/repo",
            Pages = new List<WikiPage> { new WikiPage { Id = "Page1", Title = "Page 1", Content = "Content 1" } }
        };
        var resultJson = JsonSerializer.Serialize(wikiStructure);

        var jobId = Guid.NewGuid().ToString();
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_testDbPath}"))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(@"
                INSERT INTO GenerationJobs (Id, RepoPath, Status, ProgressPercentage, Message, CreatedAt, LastUpdated, ResultJson)
                VALUES (@Id, @RepoPath, @Status, @ProgressPercentage, @Message, @CreatedAt, @LastUpdated, @ResultJson)",
                new
                {
                    Id = jobId,
                    RepoPath = "/test/repo",
                    Status = GenerationStatus.Completed,
                    ProgressPercentage = 100,
                    Message = "Completed",
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    LastUpdated = DateTime.UtcNow.ToString("O"),
                    ResultJson = resultJson
                });
        }

        // Setup mock services for the judge phase
        var mockRubricService = new Mock<IRubricGenerationService>();
        var mockJudgeService = new Mock<IDocumentationJudgeService>();

        var rubric = new EvaluationRubric { Title = "Test Rubric" };
        mockRubricService.Setup(x => x.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);
        
        mockJudgeService.Setup(x => x.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment>());

        // 2. Act: Run the processing logic
        int processedCount = await ProcessAllJobsAsync(mockRubricService.Object, mockJudgeService.Object);

        // 3. Assert
        Assert.That(processedCount, Is.EqualTo(1));
        mockRubricService.Verify(x => x.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()), Times.Once);
        mockJudgeService.Verify(x => x.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunJudgementOnAllJobs_ShouldGenerateDetailedReport()
    {
        // 1. Arrange: Insert multiple jobs
        await InsertJobAsync("job1", "/repo/A", 0.75);
        await InsertJobAsync("job2", "/repo/B", 0.85);

        var mockRubricService = new Mock<IRubricGenerationService>();
        var mockJudgeService = new Mock<IDocumentationJudgeService>();

        mockRubricService.Setup(x => x.GenerateRubricAsync(It.IsAny<WikiStructure>(), It.IsAny<RepositoryInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationRubric { Title = "Test Rubric" });
        
        mockJudgeService.SetupSequence(x => x.EvaluateRequirementsAsync(It.IsAny<List<RubricRequirement>>(), It.IsAny<WikiStructure>(), It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequirementAssessment> { new RequirementAssessment { RequirementId = "R1", MeanScore = 0.8 } })
            .ReturnsAsync(new List<RequirementAssessment> { new RequirementAssessment { RequirementId = "R1", MeanScore = 0.9 } });

        // 2. Act
        var results = await ProcessAllJobsDetailedAsync(mockRubricService.Object, mockJudgeService.Object);

        // 3. Assert
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].NewScore, Is.EqualTo(0.8));
        Assert.That(results[1].NewScore, Is.EqualTo(0.9));
        
        // Verify Summary is printed (via Console)
        PrintReport(results);
    }

    private async Task InsertJobAsync(string id, string repoPath, double score)
    {
        var wikiStructure = new WikiStructure { Title = $"Structure for {repoPath}", RepoPath = repoPath };
        var resultJson = JsonSerializer.Serialize(wikiStructure);

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            INSERT INTO GenerationJobs (Id, RepoPath, Status, ProgressPercentage, Message, CreatedAt, LastUpdated, ResultJson)
            VALUES (@Id, @RepoPath, @Status, @ProgressPercentage, @Message, @CreatedAt, @LastUpdated, @ResultJson)",
            new
            {
                Id = id,
                RepoPath = repoPath,
                Status = GenerationStatus.Completed,
                ProgressPercentage = 100,
                Message = "Completed",
                CreatedAt = DateTime.UtcNow.ToString("O"),
                LastUpdated = DateTime.UtcNow.ToString("O"),
                ResultJson = resultJson
            });
    }

    private async Task<List<JobJudgmentResult>> ProcessAllJobsDetailedAsync(IRubricGenerationService rubricService, IDocumentationJudgeService judgeService)
    {
        var results = new List<JobJudgmentResult>();
        var jobs = await _jobManager.ListAllJobsAsync();

        foreach (var job in jobs.Where(j => j.Status == GenerationStatus.Completed && !string.IsNullOrEmpty(j.ResultJson)))
        {
            try
            {
                var structure = JsonSerializer.Deserialize<WikiStructure>(job.ResultJson!);
                if (structure == null) continue;

                var repoInfo = new RepositoryInfo { Name = Path.GetFileName(job.RepoPath), RepoPath = job.RepoPath };
                var rubric = await rubricService.GenerateRubricAsync(structure, repoInfo);
                var requirements = ExtractRequirements(rubric);
                
                var assessments = await judgeService.EvaluateRequirementsAsync(requirements, structure, new List<string> { "default" });
                var newScore = assessments.Any() ? assessments.Average(a => a.MeanScore) : 0;

                results.Add(new JobJudgmentResult
                {
                    JobId = job.Id,
                    RepoPath = job.RepoPath,
                    NewScore = newScore
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing job {job.Id}: {ex.Message}");
            }
        }
        return results;
    }

    private void PrintReport(List<JobJudgmentResult> results)
    {
        Console.WriteLine("\n=== Bulk Judgment Report ===");
        Console.WriteLine($"{"Job ID",-36} | {"Repo Path",-40} | {"New Score",-10}");
        Console.WriteLine(new string('-', 95));
        foreach (var res in results)
        {
            Console.WriteLine($"{res.JobId,-36} | {res.RepoPath,-40} | {res.NewScore,10:F2}");
        }
        Console.WriteLine("============================\n");
    }

    [Test]
    [Explicit("Run this manually to process the real database at data/codeMRI.db")]
    public async Task RunJudgementOnRealDatabase_Explicit()
    {
        // 1. Arrange: Setup for real DB
        // Use the default path from DbGenerationJobManager if not specified, 
        // but often the real data is in a specific folder.
        var realDbPath = "/Users/levente/AI/codeMRI/data/sqlite/codemri.db"; // Placeholder - adjust as needed
        
        if (!File.Exists(realDbPath))
        {
            Assert.Ignore($"Real database not found at {realDbPath}");
            return;
        }

        var realJobManager = new DbGenerationJobManager(
            new Mock<ILogger<DbGenerationJobManager>>().Object, 
            _messageBus, 
            _mockScopeFactory.Object, 
            realDbPath);

        // For a real run, you'd need real service implementations as well.
        // This is a template for the user to fill with their actual service wiring if they want to run it from here.
        Assert.Inconclusive("This test is a template. To run against real data, configure your services and DB path.");
    }

    private class JobJudgmentResult
    {
        public string JobId { get; set; } = string.Empty;
        public string RepoPath { get; set; } = string.Empty;
        public double NewScore { get; set; }
    }

    private async Task<int> ProcessAllJobsAsync(IRubricGenerationService rubricService, IDocumentationJudgeService judgeService)
    {
        var results = await ProcessAllJobsDetailedAsync(rubricService, judgeService);
        return results.Count;
    }

    private List<RubricRequirement> ExtractRequirements(EvaluationRubric rubric)
    {
        var list = new List<RubricRequirement>();
        void Visit(RubricNode node)
        {
            if (node is RubricRequirement r && node.IsLeaf) list.Add(r);
            if (node.Children != null)
                foreach (var c in node.Children)
                    Visit(c);
        }
        Visit(rubric);
        return list;
    }
}
