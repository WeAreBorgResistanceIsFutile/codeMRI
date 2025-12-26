using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR.Client;
using codeMRI.Core.Models;

namespace codeMRI.CLI;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("codeMRI CLI - Code Analysis and Documentation");

        // Add subcommands
        rootCommand.AddCommand(CreateWikiCommand());
        rootCommand.AddCommand(CreateTestDecompositionCommand());

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateWikiCommand()
    {
        var wikiCommand = new Command("wiki", "Generate documentation wiki for a repository");

        var inputOption = new Option<string?>(
                new[] { "--input", "-i" },
                "Path to local repository or Git URL"); 
                // Made optional because --resume doesn't need input

        var serverOption = new Option<string>(
            new[] { "--server", "-s" },
            description: "URL of the codeMRI Server",
            getDefaultValue: () => "http://localhost:5247");

        var verboseOption = new Option<bool>(
            new[] { "--verbose", "-v" },
            "Enable verbose logging");

        var forceOption = new Option<bool>(
            new[] { "--force", "-f" },
            "Force regeneration of documentation (ignore cache)");

        var outputOption = new Option<string?>(
            new[] { "--output", "-o" },
            "Directory to save the generated Markdown files");

        var audienceOption = new Option<string>(
            new[] { "--audience", "-a" },
            description: "Target audience for documentation generation (Developer, Tester, DevOps)",
            getDefaultValue: () => "Developer");
            
        var resumeOption = new Option<string?>(
            new[] { "--resume" },
            "Resume tracking an existing generation job by ID");

        var listOption = new Option<bool>(
            new[] { "--list" },
            "List all active generation jobs on the server");

        wikiCommand.AddOption(inputOption);
        wikiCommand.AddOption(serverOption);
        wikiCommand.AddOption(verboseOption);
        wikiCommand.AddOption(forceOption);
        wikiCommand.AddOption(outputOption);
        wikiCommand.AddOption(audienceOption);
        wikiCommand.AddOption(resumeOption);
        wikiCommand.AddOption(listOption);

        wikiCommand.SetHandler(
            async (input, serverUrl, verbose, force, output, audience, resumeJobId, list) =>
            {
                // Validate input or resume or list
                if (string.IsNullOrEmpty(input) && string.IsNullOrEmpty(resumeJobId) && !list)
                {
                    Console.WriteLine("Error: Either --input, --resume, or --list must be specified.");
                    return;
                }

                // Validate audience
                if (!Enum.TryParse<AudienceType>(audience, ignoreCase: true, out var audienceType))
                {
                    Console.WriteLine($"Error: Invalid audience type '{audience}'. Valid values are: Developer, Tester, DevOps");
                    return;
                }
                
                await RunWikiAsync(input, serverUrl, verbose, force, output, audienceType, resumeJobId, list);
            }, inputOption, serverOption, verboseOption, forceOption, outputOption, audienceOption, resumeOption, listOption);

        return wikiCommand;
    }

    private static Command CreateTestDecompositionCommand()
    {
        var testCommand = new Command("test-decomposition", "Test hierarchical decomposition on a repository");

        var inputOption = new Option<string>(
            new[] { "--input", "-i" },
            "Path to local repository or Git URL") { IsRequired = true };

        var outputOption = new Option<string?>(
            new[] { "--output", "-o" },
            "Output JSON file path for module tree (optional)");

        var verboseOption = new Option<bool>(
            new[] { "--verbose", "-v" },
            "Enable verbose logging");

        var visualizeOption = new Option<bool>(
            new[] { "--visualize" },
            "Generate a tree visualization");

        testCommand.AddOption(inputOption);
        testCommand.AddOption(outputOption);
        testCommand.AddOption(verboseOption);
        testCommand.AddOption(visualizeOption);

        testCommand.SetHandler(
            async (input, output, verbose, visualize) =>
            {
                await RunDecompositionTestAsync(input, output, verbose, visualize);
            }, inputOption, outputOption, verboseOption, visualizeOption);

        return testCommand;
    }

    private static async Task RunWikiAsync(string? input, string serverUrl, bool verbose, bool force, string? output, AudienceType audience, string? resumeJobId, bool list)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromHours(2); // Increased timeout even for polling loop safety

        if (verbose) Console.WriteLine($"Connecting to {serverUrl}...");

        if (list)
        {
            try 
            {
                var response = await client.GetAsync("api/Wiki/generations/active");
                if (response.IsSuccessStatusCode)
                {
                    var jobs = await response.Content.ReadFromJsonAsync<List<GenerationJob>>();
                    Console.WriteLine($"Active Jobs ({jobs?.Count ?? 0}):");
                    Console.WriteLine("--------------------------------------------------------------------------------");
                    Console.WriteLine($"{"Job ID",-38} | {"Status",-12} | {"Progress",-8} | {"Repo"}");
                    Console.WriteLine("--------------------------------------------------------------------------------");
                    
                    if (jobs != null)
                    {
                        foreach (var job in jobs)
                        {
                            Console.WriteLine($"{job.Id,-38} | {job.Status,-12} | {job.ProgressPercentage + "%",-8} | {job.RepoPath}");
                        }
                    }
                    Console.WriteLine("--------------------------------------------------------------------------------");
                    Console.WriteLine("Use --resume <JobId> to resume tracking a specific job.");
                }
                else
                {
                    Console.WriteLine($"Error retrieving jobs: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to server: {ex.Message}");
            }
            return;
        }

        string jobId;
        string? targetPath = input;
        bool isTemp = false;

        if (!string.IsNullOrEmpty(resumeJobId))
        {
            jobId = resumeJobId;
            Console.WriteLine($"Resuming tracking for Job ID: {jobId}");
        }
        else
        {
            // Start New Job
            if (string.IsNullOrEmpty(input)) return; // Should be caught by handler check

            // 1. Handle Git Cloning (Client-side preparation)
            if (GitHelper.IsGitUrl(input))
            {
                try
                {
                    if (verbose) Console.WriteLine($"Cloning {input}...");
                    targetPath = await GitHelper.CloneRepositoryAsync(input);
                    isTemp = true;
                    if (verbose) Console.WriteLine($"Cloned to {targetPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cloning repository: {ex.Message}");
                    return;
                }
            }
            else
            {
                targetPath = Path.GetFullPath(input);
                if (!Directory.Exists(targetPath))
                {
                    Console.WriteLine($"Error: Directory not found: {targetPath}");
                    return;
                }
            }

            // 2. Call Server to Start Job
            var request = new
            {
                RepoPath = targetPath,
                Language = "Detected automatically",
                ForceRegenerate = force,
                SkipPersistence = !string.IsNullOrEmpty(output),
                ConnectionId = (string?)null, // SignalR not strictly needed with polling, but could be added
                Audience = audience.ToString()
            };

            try
            {
                if (verbose) Console.WriteLine($"Starting generation job for {targetPath}...");
                var response = await client.PostAsJsonAsync("api/Wiki/generate-async", request);
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error starting job: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return;
                }

                var jobInfo = await response.Content.ReadFromJsonAsync<JsonElement>();
                jobId = jobInfo.GetProperty("jobId").GetString() ?? string.Empty;
                Console.WriteLine($"Job started. Job ID: {jobId}");
                Console.WriteLine($"You can resume tracking this job later with: --resume {jobId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting job: {ex.Message}");
                return;
            }
        }

        // 3. Poll for Status
        Console.WriteLine("Waiting for completion...");
        bool isComplete = false;
        GenerationJob? finalJobState = null;

        while (!isComplete)
        {
            try
            {
                var statusResponse = await client.GetAsync($"api/Wiki/generation/{jobId}");
                if (!statusResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error checking status: {statusResponse.StatusCode}");
                    await Task.Delay(5000);
                    continue;
                }

                var job = await statusResponse.Content.ReadFromJsonAsync<GenerationJob>();
                if (job == null) break;

                finalJobState = job;

                // Simple progress display
                Console.Write($"\r[{job.Status}] {job.Message} ({job.ProgressPercentage}%)   ");

                if (job.Status == GenerationStatus.Completed || 
                    job.Status == GenerationStatus.Failed || 
                    job.Status == GenerationStatus.Cancelled)
                {
                    isComplete = true;
                    Console.WriteLine(); // New line after loop
                }
                else
                {
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError polling status: {ex.Message}");
                await Task.Delay(5000);
            }
        }

        // 4. Retrieve Result
        if (finalJobState?.Status == GenerationStatus.Completed)
        {
            Console.WriteLine("Generation complete. Retrieving results...");
            try
            {
                var resultResponse = await client.GetAsync($"api/Wiki/generation/{jobId}/result");
                if (resultResponse.IsSuccessStatusCode)
                {
                    // If output directory is specified, save files
                    if (!string.IsNullOrEmpty(output))
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var structure = await resultResponse.Content.ReadFromJsonAsync<WikiStructure>(options);
                        
                         if (structure != null)
                        {
                            // Create audience-specific subdirectory
                            var outputDir = Path.Combine(output, audience.ToString());
                            Directory.CreateDirectory(outputDir);
                            
                            Console.WriteLine($"Received {structure.Pages.Count} pages from server.");
                            if (verbose)
                            {
                                Console.WriteLine("Pages:");
                                foreach (var p in structure.Pages) Console.WriteLine($"- {p.Title}");
                            }

                            Console.WriteLine($"Saving pages to {outputDir}...");

                            foreach (var page in structure.Pages)
                            {
                                var safeTitle = string.Join("_", page.Title.Split(Path.GetInvalidFileNameChars()));
                                var filePath = Path.Combine(outputDir, $"{safeTitle}.md");
                                await File.WriteAllTextAsync(filePath, page.Content);
                            }

                            Console.WriteLine($"Saved {structure.Pages.Count} files to {outputDir}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Job completed successfully. Use --output to save files, or view in Web UI.");
                         if (verbose)
                        {
                             var json = await resultResponse.Content.ReadAsStringAsync();
                             Console.WriteLine("Result: " + (json.Length > 1000 ? json.Substring(0, 1000) + "..." : json));
                        }
                    }
                }
                else
                {
                     Console.WriteLine($"Error retrieving result: {resultResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error processing result: {ex.Message}");
                 if (verbose) Console.WriteLine(ex.StackTrace);
            }
        }
        else if (finalJobState?.Status == GenerationStatus.Failed)
        {
            Console.WriteLine($"Job failed: {finalJobState.Error}");
        }

        if (isTemp && !string.IsNullOrEmpty(targetPath))
        {
            if (verbose) Console.WriteLine($"Note: Repository was cloned to: {targetPath}");
        }
    }

    private static async Task RunDecompositionTestAsync(string input, string? output, bool verbose, bool visualize)
    {
        var repositoryPath = input;
        var isTemp = false;

        try
        {
            // Handle Git URLs
            if (GitHelper.IsGitUrl(input))
            {
                if (verbose) Console.WriteLine($"Cloning {input}...");
                repositoryPath = await GitHelper.CloneRepositoryAsync(input);
                isTemp = true;
                if (verbose) Console.WriteLine($"Cloned to {repositoryPath}");
            }
            else
            {
                repositoryPath = Path.GetFullPath(input);
                if (!Directory.Exists(repositoryPath))
                {
                    Console.WriteLine($"Error: Directory not found: {repositoryPath}");
                    return;
                }
            }

            // Run decomposition
            var moduleTree = await DecompositionTester.RunDecompositionAsync(repositoryPath, "http://localhost:5247", verbose);

            if (moduleTree == null)
            {
                Console.WriteLine("Decomposition failed or returned no results.");
                return;
            }

            // Print tree visualization
            if (visualize)
            {
                DecompositionTester.PrintModuleTree(moduleTree, verbose);
            }

            // Print statistics
            DecompositionTester.PrintStatistics(moduleTree);

            // Save to JSON if requested
            if (!string.IsNullOrEmpty(output))
            {
                await DecompositionTester.SaveToJsonAsync(moduleTree, output);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (verbose) Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            if (isTemp && verbose)
            {
                Console.WriteLine($"\nNote: Repository was cloned to: {repositoryPath}");
            }
        }
    }
}