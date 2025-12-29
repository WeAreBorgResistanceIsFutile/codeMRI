using System.Net.Http.Json;
using codeMRI.Core.Models;
using codeMRI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace codeMRI.Server.Tests;

[TestFixture]
public class WikiControllerTests
{
    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../.."));
        var serverProjectPath = Path.Combine(projectRoot, "codeMRI.Server");
        var dbPath = Path.Combine(projectRoot, "data/sqlite/test_wiki_controller.db");
        var connectionString = $"Data Source={dbPath}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(serverProjectPath);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WikiDb"] = connectionString,
                    ["ConnectionStrings:IngestionDb"] = connectionString,
                });
            });
        });
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
        _client.Dispose();
    }

    [Test]
    public async Task SavePage_ShouldUpdateExistingPage()
    {
        // Arrange
        var repoPath = "/test/edit/repo";
        var pageId = "edit-test-page";
        var originalPage = new WikiPage
        {
            Id = pageId,
            Title = "Original Title",
            Content = "Original Content"
        };

        // Pre-save using the repo service to have something to edit
        using (var scope = _factory.Services.CreateScope())
        {
            var wikiRepo = scope.ServiceProvider.GetRequiredService<IWikiRepository>();
            await wikiRepo.SavePageAsync(repoPath, originalPage);
        }

        var updatedPage = new WikiPage
        {
            Id = pageId,
            Title = "Updated Title",
            Content = "Updated Content"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/Wiki/page/save?repoPath={Uri.EscapeDataString(repoPath)}", updatedPage);

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Verify via Repository
        using (var scope = _factory.Services.CreateScope())
        {
            var wikiRepo = scope.ServiceProvider.GetRequiredService<IWikiRepository>();
            var result = await wikiRepo.GetPageAsync(repoPath, pageId);
            result.Should().NotBeNull();
            result!.Title.Should().Be("Updated Title");
            result!.Content.Should().Be("Updated Content");
        }
    }
}
