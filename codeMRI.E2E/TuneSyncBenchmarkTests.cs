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
    private string _cacheDirectory;
    private string _repoPath;
    private string _referenceCachePath;
    private const string RepoUrl = "https://github.com/WilliamNT/tunesynctool";
    private const string ReferenceUrl = "https://deepwiki.com/WilliamNT/tunesynctool";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Stable cache directory
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "codeMRI_Benchmark_Cache");
        Directory.CreateDirectory(_cacheDirectory);

        _repoPath = Path.Combine(_cacheDirectory, "tunesynctool");
        _referenceCachePath = Path.Combine(_cacheDirectory, "reference_corpus.txt");

        // 1. Clone the repository (Cached)
        if (Directory.Exists(_repoPath) && Directory.GetFiles(_repoPath).Length > 0)
        {
            TestContext.WriteLine($"Repository cache found at {_repoPath}. Skipping clone.");
        }
        else
        {
            TestContext.WriteLine($"Cloning {RepoUrl} to {_repoPath}...");
            if (Directory.Exists(_repoPath)) Directory.Delete(_repoPath, true);
            Directory.CreateDirectory(_repoPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone {RepoUrl} .",
                WorkingDirectory = _repoPath,
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
        // Do NOT delete the cache directory to enable persistence across runs
    }

    [Test]
    [Timeout(600000)] // 10 minutes
    public async Task Benchmark_TuneSyncTool_DocumentationQuality()
    {
        var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        // 1. Ingest
        TestContext.WriteLine("Starting Ingestion..."); 
        var ingestResponse = await client.PostAsJsonAsync("/api/ingest", new IngestRequest { RepoPath = _repoPath });
        ingestResponse.EnsureSuccessStatusCode();
        TestContext.WriteLine("Ingestion Complete.");

        // 2. Generate Structure
        TestContext.WriteLine("Generating Structure...");
        var structureResponse = await client.PostAsJsonAsync("/api/wiki/structure", new StructureRequest 
        { 
            RepoPath = _repoPath,
            ForceRegenerate = true 
        });
        structureResponse.EnsureSuccessStatusCode();
        var structure = await structureResponse.Content.ReadFromJsonAsync<codeMRI.Server.Api.WikiStructure>();
        Assert.That(structure, Is.Not.Null);
        
        // 3. Find relevant page (Overview/Readme)
        var targetPageRequest = new PageGenerationRequest 
        { 
            RepoPath = _repoPath,
            Title = "Overview", // Usually the main entry point
            ForceRegenerate = true,
            FilePaths = new List<string> { "README.md" } // Hint to focus on README if possible
        };

        TestContext.WriteLine("Generating Page 'Overview'...");
        var pageResponse = await client.PostAsJsonAsync("/api/wiki/page", targetPageRequest);
        pageResponse.EnsureSuccessStatusCode();
        var generatedPageApi = await pageResponse.Content.ReadFromJsonAsync<codeMRI.Server.Api.WikiPage>();
        Assert.That(generatedPageApi, Is.Not.Null);

        // 4. Fetch Reference Content (Cached Recursive Scraping)
        string referenceText;
        if (File.Exists(_referenceCachePath))
        {
            TestContext.WriteLine($"Loading reference corpus from cache: {_referenceCachePath}");
            referenceText = await File.ReadAllTextAsync(_referenceCachePath);
        }
        else
        {
            TestContext.WriteLine("Fetching Reference Corpus from DeepWiki (Recursive)...");
            referenceText = await CrawlDeepWikiRecursively(ReferenceUrl);
            await File.WriteAllTextAsync(_referenceCachePath, referenceText);
            TestContext.WriteLine($"Fetched and cached {referenceText.Length} chars of reference content.");
        }
        
        // 5. Evaluate
        TestContext.WriteLine("Starting Evaluation...");
        using var scope = _factory.Services.CreateScope();
        var judgeService = scope.ServiceProvider.GetRequiredService<IDocumentationJudgeService>();

        var requirement = new RubricRequirement
        {
            Title = "Accuracy against Reference",
            Description = $"The generated documentation must match the purpose and details described in the reference corpus (first 4k chars): {referenceText.Substring(0, Math.Min(referenceText.Length, 4000))}..." 
        };

        // Map API model to Core model for the Judge
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

    private async Task<string> CrawlDeepWikiRecursively(string startUrl)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        var aggregatedContent = new System.Text.StringBuilder();
        using var httpClient = new HttpClient();
        
        // Base domain scope for crawling
        var baseDomain = new Uri(startUrl).Host;
        // Path restriction to stay within the repo docs
        var basePath = new Uri(startUrl).AbsolutePath;

        queue.Enqueue(startUrl);
        visited.Add(startUrl);

        int maxPages = 10; // Safety limit
        int pagesFetched = 0;

        while (queue.Count > 0 && pagesFetched < maxPages)
        {
            var currentUrl = queue.Dequeue();
            TestContext.WriteLine($"Crawling: {currentUrl}");

            try
            {
                var html = await httpClient.GetStringAsync(currentUrl);
                pagesFetched++;

                // 1. Extract Text
                var text = ExtractTextFromHtml(html);
                aggregatedContent.AppendLine($"--- PAGE: {currentUrl} ---");
                aggregatedContent.AppendLine(text);
                aggregatedContent.AppendLine();

                // 2. Extract Links
                var matches = System.Text.RegularExpressions.Regex.Matches(html, "href=\"([^\"]+)\"");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var href = match.Groups[1].Value;
                    
                    Uri nextUri;
                    try 
                    {
                        nextUri = new Uri(new Uri(currentUrl), href);
                    }
                    catch { continue; }

                    if (nextUri.Host != baseDomain) continue;
                    if (!nextUri.AbsolutePath.StartsWith(basePath)) continue;
                    
                    var cleanUrl = nextUri.GetLeftPart(UriPartial.Path);

                    if (!visited.Contains(cleanUrl))
                    {
                        visited.Add(cleanUrl);
                        queue.Enqueue(cleanUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Failed to crawl {currentUrl}: {ex.Message}");
            }
        }

        return aggregatedContent.ToString();
    }

    private string ExtractTextFromHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<style.*?>.*?</style>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<script.*?>.*?</script>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
    }
}
