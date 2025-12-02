using System.Net.Http.Json;
using codeMRI.Shared.DTOs;
using codeMRI.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace codeMRI.E2E;

[TestFixture]
public class StructureTests
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
    public async Task GenerateStructure_OllamaRAG5_ReturnsValidStructure()
    {
        // Arrange
        var client = _factory!.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5); // Structure generation involves LLM, can be slow

        var request = new StructureRequest
        {
            RepoPath = "/Users/levente/AI/OllamaRAG5",
            ReadmeContent = "This is a RAG application using Ollama and Qdrant.",
            ForceRegenerate = true // Force generation to test the LLM interaction
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/wiki/structure", request);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Structure generation failed. Status: {response.StatusCode}. Error: {error}");
        }

        response.EnsureSuccessStatusCode();

        var structure = await response.Content.ReadFromJsonAsync<WikiStructure>();
        structure.Should().NotBeNull();
        structure!.Title.Should().NotBeNullOrEmpty();
        structure.Sections.Should().NotBeNull();
        // We expect at least one section if the LLM works correctly
        structure.Sections.Should().HaveCountGreaterThan(0);

        TestContext.WriteLine($"Generated Title: {structure.Title}");
        TestContext.WriteLine($"Generated {structure.Sections.Count} sections.");
    }
}