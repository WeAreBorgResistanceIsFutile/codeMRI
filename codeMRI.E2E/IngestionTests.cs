using System.Net.Http.Json;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace codeMRI.E2E;

[TestFixture]
public class IngestionTests
{
    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;

    [Test]
    [Ignore("")]
    public async Task Ingest_OllamaRAG5_ReturnsSuccess()
    {
        // Arrange
        var client = _factory!.CreateClient();
        // Use a generous timeout as ingestion can take time depending on repo size and embedding speed
        client.Timeout = TimeSpan.FromMinutes(5);

        var request = new IngestRequest
        {
            RepoPath = "/Users/levente/AI/OllamaRAG5"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/ingest", request);

        // Assert
        // If this fails with 500, check if Ollama/Qdrant are running and accessible.
        // If it fails with 400, check if the directory exists.
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Ingestion failed. Status: {response.StatusCode}. Error: {errorContent}");
        }

        response.EnsureSuccessStatusCode();
    }
}