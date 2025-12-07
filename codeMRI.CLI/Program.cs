using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json;

namespace codeMRI.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("codeMRI CLI (Thin Client)");

        var inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Path to local repository or Git URL")
        { IsRequired = true };

        var serverOption = new Option<string>(
            aliases: new[] { "--server", "-s" },
            description: "URL of the codeMRI Server",
            getDefaultValue: () => "http://localhost:5247");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging");

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "Force regeneration of documentation (ignore cache)");

        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Directory to save the generated Markdown files");

        rootCommand.AddOption(inputOption);
        rootCommand.AddOption(serverOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(forceOption);
        rootCommand.AddOption(outputOption);

        rootCommand.SetHandler(async (string input, string serverUrl, bool verbose, bool force, string? output) =>
        {
            await RunAsync(input, serverUrl, verbose, force, output);
        }, inputOption, serverOption, verboseOption, forceOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunAsync(string input, string serverUrl, bool verbose, bool force, string? output)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for generation

        if (verbose) Console.WriteLine($"Connecting to {serverUrl}...");

        string targetPath = input;
        bool isTemp = false;

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

        // 2. Call Server
        var request = new
        {
            RepoPath = targetPath,
            Language = "Detected automatically",
            ForceRegenerate = force,
            SkipPersistence = !string.IsNullOrEmpty(output)
        };

        try
        {
            if (verbose) Console.WriteLine($"Sending request to server for {targetPath}...");

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
                            Directory.CreateDirectory(output);
                            Console.WriteLine($"Received {structure.Pages.Count} pages from server.");
                            if (verbose)
                            {
                                Console.WriteLine("Pages:");
                                foreach (var p in structure.Pages) Console.WriteLine($"- {p.Title}");
                            }
                            
                            Console.WriteLine($"Saving pages to {output}...");
                            
                            foreach(var page in structure.Pages)
                            {
                                var safeTitle = string.Join("_", page.Title.Split(Path.GetInvalidFileNameChars()));
                                var filePath = Path.Combine(output, $"{safeTitle}.md");
                                await File.WriteAllTextAsync(filePath, page.Content);
                            }
                            Console.WriteLine($"Saved files to {output}");
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
                 Console.WriteLine("It is required for viewing file contents in the UI. Do not delete it manually if you plan to browse source code.");
            }
        }
    }
}