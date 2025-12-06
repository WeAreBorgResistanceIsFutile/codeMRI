using System.Net.Http.Json;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace codeMRI.E2E;

[TestFixture]
public class FrontendApiIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private const string TestRepoPath = "/Users/levente/AI/OllamaRAG5";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromMinutes(5); // Ingestion can take time
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    [Order(2)]
    public async Task GenerateStructure_WithValidPath_ReturnsStructure()
    {
        // Arrange
        var request = new StructureRequest
        {
            RepoPath = TestRepoPath,
            ReadmeContent = "# Test Readme",
            Language = "English",
            ForceRegenerate = true 
        };

        // Act
        var response = await _client.PostAsJsonAsync("api/wiki/structure", request);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"GenerateStructure failed: {response.StatusCode} - {content}");
        }
        Assert.That(response.IsSuccessStatusCode, Is.True, "Structure API should return success.");
        
        var structure = await response.Content.ReadFromJsonAsync<WikiStructure>();
        Assert.That(structure, Is.Not.Null);
        // Assert.That(structure.Root, Is.Not.Null); // Depending on implementation
    }

    [Test]
    [Order(3)]
    public async Task GeneratePage_WithValidPathAndFiles_ReturnsPage()
    {
        // Arrange
        // We need at least one valid file path from the repo to test this effectively.
        // Assuming the repo has at least one file. Ideally we'd get this from the structure test.
        var filePaths = new List<string> { "README.md" }; // Common file, likely to exist

        var request = new PageGenerationRequest
        {
            RepoPath = TestRepoPath,
            Title = "Test Page",
            FilePaths = filePaths,
            Language = "English",
            ForceRegenerate = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("api/wiki/page", request);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"GeneratePage failed: {response.StatusCode} - {content}");
        }
        Assert.That(response.IsSuccessStatusCode, Is.True, "Page Generation API should return success.");

        var page = await response.Content.ReadFromJsonAsync<WikiPage>();
        Assert.That(page, Is.Not.Null);
        Assert.That(page.Title, Is.EqualTo("Test Page"));
    }
}
