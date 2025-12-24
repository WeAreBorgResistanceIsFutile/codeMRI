using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR.Client;

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

        var inputOption = new Option<string>(
                new[] { "--input", "-i" },
                "Path to local repository or Git URL")
            { IsRequired = true };

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

        wikiCommand.AddOption(inputOption);
        wikiCommand.AddOption(serverOption);
        wikiCommand.AddOption(verboseOption);
        wikiCommand.AddOption(forceOption);
        wikiCommand.AddOption(outputOption);
        wikiCommand.AddOption(audienceOption);

        wikiCommand.SetHandler(
            async (input, serverUrl, verbose, force, output, audience) =>
            {
                // Validate audience
                if (!Enum.TryParse<AudienceType>(audience, ignoreCase: true, out var audienceType))
                {
                    Console.WriteLine($"Error: Invalid audience type '{audience}'. Valid values are: Developer, Tester, DevOps");
                    return;
                }
                
                await RunWikiAsync(input, serverUrl, verbose, force, output, audienceType);
            }, inputOption, serverOption, verboseOption, forceOption, outputOption, audienceOption);

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

    private static async Task RunWikiAsync(string input, string serverUrl, bool verbose, bool force, string? output, AudienceType audience)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for generation

        if (verbose) Console.WriteLine($"Connecting to {serverUrl}...");

        var targetPath = input;
        var isTemp = false;

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

        // 2. Connect to SignalR Hub for progress updates
        await using var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/wikiHub")
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<ProgressInfo>("ReceiveProgress", info =>
        {
            // Clear current line if possible to make it look like a progress bar, or just write lines
            // Simple approach: [Phase] Message (Percentage%)
            Console.WriteLine($"[{info.Phase}] {info.Message} ({info.Percentage}%)");
        });

        string? connectionId = null;
        try
        {
            if (verbose) Console.WriteLine("Connecting to progress hub...");
            await hubConnection.StartAsync();
            connectionId = hubConnection.ConnectionId;
            if (verbose) Console.WriteLine($"Connected to hub. ID: {connectionId}");
        }
        catch (Exception ex)
        {
            if (verbose)
                Console.WriteLine(
                    $"Warning: Could not connect to progress hub: {ex.Message}. functionality will be limited.");
        }

        // 3. Call Server
        var request = new
        {
            RepoPath = targetPath,
            Language = "Detected automatically",
            ForceRegenerate = force,
            SkipPersistence = !string.IsNullOrEmpty(output),
            ConnectionId = connectionId,
            Audience = audience.ToString()
        };

        try
        {
            if (verbose) Console.WriteLine($"Sending request to server for {targetPath}...");
            
            // Debug: Show the JSON being sent
            if (verbose)
            {
                var debugJson = System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"Request JSON:\n{debugJson}");
            }

            var response = await client.PostAsJsonAsync("api/Wiki/generate-advanced", request);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Success! Documentation generated.");

                if (!string.IsNullOrEmpty(output))
                {
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var structure = await response.Content.ReadFromJsonAsync<WikiStructure>(options);

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
                        else
                        {
                            Console.WriteLine("Warning: Received empty structure from server.");
                        }
                    }
                    catch (JsonException jex)
                    {
                        Console.WriteLine($"Error parsing response for file output: {jex.Message}");
                        if (verbose) Console.WriteLine(await response.Content.ReadAsStringAsync());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving files: {ex.Message}");
                    }
                }
                else
                {
                    if (verbose)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Server Response: " + json);
                    }
                    else
                    {
                        Console.WriteLine("You can view it now in the Web UI.");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Server Error: {response.StatusCode}");
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine(error);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error connecting to server: {ex.Message}");
            Console.WriteLine("Is codeMRI.Server running?");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
        finally
        {
            if (isTemp)
            {
                Console.WriteLine($"Note: Repository was cloned to temporary path: {targetPath}");
                Console.WriteLine(
                    "It is required for viewing file contents in the UI. Do not delete it manually if you plan to browse source code.");
            }
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