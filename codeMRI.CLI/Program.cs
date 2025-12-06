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

        rootCommand.AddOption(inputOption);
        rootCommand.AddOption(serverOption);
        rootCommand.AddOption(verboseOption);

        rootCommand.SetHandler(async (string input, string serverUrl, bool verbose) =>
        {
            await RunAsync(input, serverUrl, verbose);
        }, inputOption, serverOption, verboseOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunAsync(string input, string serverUrl, bool verbose)
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
            ForceRegenerate = false
        };

        try
        {
            if (verbose) Console.WriteLine($"Sending request to server for {targetPath}...");

            var response = await client.PostAsJsonAsync("api/Wiki/generate-advanced", request);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Success! Documentation generated.");
                if (verbose)
                {
                    // Optionally try to parse response to show stats
                     var json = await response.Content.ReadAsStringAsync();
                     Console.WriteLine("Server Response: " + json);
                }
                else
                {
                     Console.WriteLine("You can view it now in the Web UI.");
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
            // We do NOT delete the temp dir here immediately if the server needs to read it?
            // Wait, if the server is local, it reads `targetPath`.
            // If we delete `targetPath` now, the server checks might fail if it does lazy loading?
            // But `generate-advanced` waits until completion. So it should be safe to delete IF the server has persisted everything.
            // However, the Server stores `RepoPath` in the DB. If future requests need to read files from disk (e.g. valid links), the files must exist.
            // If `isTemp`, we probably want to keep it or warn the user.
            // For a system tool context, usually the repo exists.
            
            if (isTemp)
            {
                 Console.WriteLine($"Note: Repository was cloned to temporary path: {targetPath}");
                 Console.WriteLine("It is required for viewing file contents in the UI. Do not delete it manually if you plan to browse source code.");
            }
        }
    }
}
