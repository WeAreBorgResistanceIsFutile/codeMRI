using System.Diagnostics;
using System.Net.Http.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace codeMRI.E2E;

[TestFixture]
[Category("Benchmark")]
public class TuneSyncBenchmarkTests
{
    private WebApplicationFactory<Program> _factory;
    private string _tempRepoPath;
    private const string RepoUrl = "https://github.com/WilliamNT/tunesynctool";
    private const string ReferenceUrl = "https://deepwiki.com/WilliamNT/tunesynctool";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // 1. Clone the repository
        _tempRepoPath = Path.Combine(Path.GetTempPath(), "tunesynctool_bench_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRepoPath);
        
        TestContext.WriteLine($"Cloning {RepoUrl} to {_tempRepoPath}...");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone {RepoUrl} .",
            WorkingDirectory = _tempRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process!.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new Exception($"Git clone failed: {error}");
        }

        // 2. Setup Factory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Configure specific test settings if needed
            });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
        
        // Clean up repo
        if (Directory.Exists(_tempRepoPath))
        {
            try
            {
                // Force delete including read-only files (like .git objects)
                DeleteDirectory(_tempRepoPath);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Warning: Failed to cleanup temp repo: {ex.Message}");
            }
        }
    }

    private void DeleteDirectory(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        Directory.Delete(path, true);
    }

    [Test]
    [Timeout(600000)] // 10 minutes
    public async Task Benchmark_TuneSyncTool_DocumentationQuality()
    {
        var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        // 1. Ingest
        TestContext.WriteLine("Starting Ingestion..."); // Modified to use TestContext.WriteLine
        var ingestResponse = await client.PostAsJsonAsync("/api/ingest", new IngestRequest { RepoPath = _tempRepoPath });
        ingestResponse.EnsureSuccessStatusCode();
        TestContext.WriteLine("Ingestion Complete.");

        // 2. Generate Structure
        TestContext.WriteLine("Generating Structure...");
        var structureResponse = await client.PostAsJsonAsync("/api/wiki/structure", new StructureRequest 
        { 
            RepoPath = _tempRepoPath,
            ForceRegenerate = true 
        });
        structureResponse.EnsureSuccessStatusCode();
        var structure = await structureResponse.Content.ReadFromJsonAsync<codeMRI.Server.Api.WikiStructure>();
        Assert.That(structure, Is.Not.Null);
        
        // 3. Find relevant page (Overview/Readme)
        // TuneSync structure usually has a root or main page. We'll try to target the overview.
        var targetPageRequest = new PageGenerationRequest 
        { 
            RepoPath = _tempRepoPath,
            Title = "Overview", // Usually the main entry point
            ForceRegenerate = true,
            FilePaths = new List<string> { "README.md" } // Hint to focus on README if possible
        };

        TestContext.WriteLine("Generating Page 'Overview'...");
        var pageResponse = await client.PostAsJsonAsync("/api/wiki/page", targetPageRequest);
        pageResponse.EnsureSuccessStatusCode();
        var generatedPageApi = await pageResponse.Content.ReadFromJsonAsync<codeMRI.Server.Api.WikiPage>();
        Assert.That(generatedPageApi, Is.Not.Null);

        // 4. Fetch Reference Content
        TestContext.WriteLine("Fetching Reference from DeepWiki...");
        using var httpClient = new HttpClient();
        // Ignoring HTML fetch for safety in this specific run if unstable, but keeping logic
        var referenceHtml = ""; 
        try 
        {
            referenceHtml = await httpClient.GetStringAsync(ReferenceUrl); 
        }
        catch
        {
            TestContext.WriteLine("Warning: Could not fetch DeepWiki, using fallback text.");
            referenceHtml = "TuneSyncTool is a utility for synchronizing music libraries.";
        }
        var referenceText = System.Text.RegularExpressions.Regex.Replace(referenceHtml, "<.*?>", " "); 
        
        // 5. Evaluate
        TestContext.WriteLine("Starting Evaluation...");
        using var scope = _factory.Services.CreateScope();
        var judgeService = scope.ServiceProvider.GetRequiredService<IDocumentationJudgeService>();

        var requirement = new RubricRequirement
        {
            Title = "Accuracy against Reference",
            Description = $"The generated documentation must match the purpose described: {referenceText.Substring(0, Math.Min(referenceText.Length, 500))}..." 
        };

        // Map API model to Core model for the Judge (Manual mapping)
        // Since DocumentationJudgeService serializes the structure to prompt, 
        // we can inject the content into the Description to ensure the LLM sees it.
        var evalStructure = new codeMRI.Core.Models.WikiStructure 
        { 
            Title = generatedPageApi.Title,
            Description = $"Content of {generatedPageApi.Title}:\n\n{generatedPageApi.Content}",
            Sections = new List<codeMRI.Core.Models.WikiSection>()
        };

        var assessment = await judgeService.EvaluateRequirementAsync(requirement, evalStructure);

        TestContext.WriteLine($"\n--- EVALUATION RESULT ---");
        TestContext.WriteLine($"Score: {assessment.MeanScore}");
        TestContext.WriteLine($"Reasoning: {string.Join(" ", assessment.Reasoning)}");
        TestContext.WriteLine($"-------------------------\n");

        // Assert
        Assert.That(assessment.MeanScore, Is.GreaterThanOrEqualTo(0.5), "Documentation quality score was too low.");
    }
}
