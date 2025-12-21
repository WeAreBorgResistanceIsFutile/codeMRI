using System.Net.Http.Json;
using codeMRI.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using FluentAssertions;

namespace codeMRI.E2E;

[TestFixture]
public class IngestionE2ETests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private System.Text.Json.JsonSerializerOptions _jsonOptions;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromMinutes(10); // Ingestion can take a long time
        
        _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task IngestElvaRepo_CompletesSuccessfully()
    {
        // Step 1: Trigger Ingestion
        var ingestionRequest = new IngestionRequest
        {
            Url = "https://github.com/WilliamNT/Elva.git",
            Audience = AudienceType.Developer
        };

        var startResponse = await _client.PostAsJsonAsync("api/wiki/ingest", ingestionRequest);
        startResponse.EnsureSuccessStatusCode();

        var startResult = await startResponse.Content.ReadFromJsonAsync<StartIngestionResponse>(_jsonOptions);
        startResult.Should().NotBeNull();
        startResult!.JobId.Should().NotBeNullOrEmpty();

        var jobId = startResult.JobId;
        TestContext.WriteLine($"Started ingestion job: {jobId}");

        // Step 2: Poll for Completion
        IngestionJob job = null;
        var timeout = TimeSpan.FromMinutes(10);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var statusResponse = await _client.GetAsync($"api/wiki/ingestion/{jobId}");
            statusResponse.EnsureSuccessStatusCode();

            job = await statusResponse.Content.ReadFromJsonAsync<IngestionJob>(_jsonOptions);
            job.Should().NotBeNull();

            TestContext.WriteLine($"Job Status: {job!.Status}, Progress: {job.ProgressPercentage}%");

            if (job.Status == IngestionStatus.Completed)
                break;

            if (job.Status == IngestionStatus.Failed)
            {
                Assert.Fail($"Ingestion job failed: {job.Error}");
            }

            await Task.Delay(5000);
        }

        job!.Status.Should().Be(IngestionStatus.Completed, "Ingestion should complete within timeout");

        // Step 3: Verify Repository Summary
        var summaryResponse = await _client.GetAsync("api/wiki/repositories-summary");
        summaryResponse.EnsureSuccessStatusCode();

        var summaries = await summaryResponse.Content.ReadFromJsonAsync<List<RepositorySummary>>(_jsonOptions);
        summaries.Should().NotBeNull();
        
        var elvaSummary = summaries!.FirstOrDefault(s => s.RemoteUrl == ingestionRequest.Url || s.Name.Contains("Elva", StringComparison.OrdinalIgnoreCase));
        elvaSummary.Should().NotBeNull("Elva repository should be in the summary");
        elvaSummary!.IsIngested.Should().BeTrue();

        var repoPath = elvaSummary.Path;
        TestContext.WriteLine($"Ingested Repo Path: {repoPath}");

        // Step 4: Verify Navigation
        var navResponse = await _client.GetAsync($"api/wiki/navigation?repoPath={Uri.EscapeDataString(repoPath)}");
        navResponse.EnsureSuccessStatusCode();

        var structure = await navResponse.Content.ReadFromJsonAsync<WikiStructure>(_jsonOptions);
        structure.Should().NotBeNull();
        structure!.Pages.Should().NotBeEmpty("There should be generated pages for the repository");
        
        TestContext.WriteLine($"Found {structure.Pages.Count} pages in navigation.");
    }
}
