using Microsoft.Extensions.Configuration;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using FluentAssertions;

namespace codeMRI.Server.Tests;

[TestFixture]
public class IngestionServerTests
{
    private WebApplicationFactory<Program> _factory = default!;
    private IServiceProvider _services = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../.."));
        var dbPath = Path.Combine(projectRoot, "data/sqlite/codemri.db");
        var connectionString = $"Data Source={dbPath}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WikiDb"] = connectionString,
                    ["ConnectionStrings:IngestionDb"] = connectionString
                });
            });
        });
        _services = _factory.Services;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
    }

    [Ignore("It takes long time to complete")]
    [TestCase("https://github.com/WeAreBorgResistanceIsFutile/codeMRI.git")]
    [TestCase("https://github.com/WilliamNT/tunesynctool.git")]
    [TestCase("https://github.com/WilliamNT/Elva.git")]
    public async Task IngestElvaRepo_CompletesSuccessfully(string repoUrl)
    {
        // Resolve services
        var ingestionManager = _services.GetRequiredService<IIngestionJobManager>();
        var wikiRepo = _services.GetRequiredService<IWikiRepository>();

        // Step 1: Trigger Ingestion
        var job = await ingestionManager.StartJobAsync(repoUrl, true, AudienceType.Developer);
        
        job.Should().NotBeNull();
        var jobId = job.Id;
        TestContext.WriteLine($"Started ingestion job: {jobId}");

        // Step 2: Poll for Completion
        IngestionJob? currentJob = null;
        var timeout = TimeSpan.FromMinutes(10);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            currentJob = await ingestionManager.GetJobAsync(jobId);
            currentJob.Should().NotBeNull();

            TestContext.WriteLine($"Job Status: {currentJob!.Status}, Progress: {currentJob.ProgressPercentage}%");

            if (currentJob.Status == IngestionStatus.Completed)
                break;

            if (currentJob.Status == IngestionStatus.Failed)
            {
                Assert.Fail($"Ingestion job failed: {currentJob.Error}");
            }

            await Task.Delay(5000);
        }

        currentJob!.Status.Should().Be(IngestionStatus.Completed, "Ingestion should complete within timeout");

        // Step 3: Verify Repository Data
        var repoPath = currentJob.RepoPath;
        repoPath.Should().NotBeNullOrEmpty();
        TestContext.WriteLine($"Ingested Repo Path: {repoPath}");

        var summaries = await wikiRepo.GetAllRepositorySummariesAsync();
        var elvaSummary = summaries.FirstOrDefault(s => s.RemoteUrl == repoUrl || s.Path == repoPath);
        elvaSummary.Should().NotBeNull("Elva repository should be in the summary");
        elvaSummary!.IsIngested.Should().BeTrue();

        // Step 4: Verify Navigation/Structure
        var structure = await wikiRepo.GetStructureAsync(repoPath);
        structure.Should().NotBeNull();
        
        var pages = await wikiRepo.GetAllPagesAsync(repoPath);
        pages.Should().NotBeEmpty("There should be generated pages for the repository");
        
        TestContext.WriteLine($"Found {pages.Count} pages for the repository.");
    }
}
