using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Core.Models;

namespace codeMRI.CLI;

/// <summary>
/// Test runner for hierarchical decomposition via Server API
/// </summary>
public static class DecompositionTester
{
    public static async Task<ModuleTree?> RunDecompositionAsync(string repositoryPath, string serverUrl = "http://localhost:5247", bool verbose = false)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromMinutes(10);

        try
        {
            if (verbose) Console.WriteLine($"Connecting to server at {serverUrl}...");
            if (verbose) Console.WriteLine($"Requesting hierarchical decomposition for: {repositoryPath}");

            var request = new { RepoPath = repositoryPath };
            var response = await client.PostAsJsonAsync("api/Decomposition/analyze", request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Server returned error: {response.StatusCode}");
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine(error);
                return null;
            }

            if (verbose) Console.WriteLine("✓ Server completed decomposition");

            var result = await response.Content.ReadFromJsonAsync<DecompositionResponse>();
            
            if (result?.ModuleTree != null)
            {
                Console.WriteLine($"✓ Received module tree with {result.ModuleTree.Nodes.Count} modules");
                return result.ModuleTree;
            }

            Console.WriteLine("Warning: Received empty module tree from server");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error connecting to server: {ex.Message}");
            Console.WriteLine("Make sure codeMRI.Server is running with: dotnet run --project codeMRI.Server");
            return null;
        }
    }

    private class DecompositionResponse
    {
        public ModuleTree ModuleTree { get; set; } = null!;
        public object? Summary { get; set; }
    }

    public static void PrintModuleTree(ModuleTree tree, bool verbose = false)
    {
        Console.WriteLine("\n=== Module Tree Structure ===\n");
        PrintNode(tree.Root, 0, verbose);
    }

    private static void PrintNode(ModuleNode node, int depth, bool verbose)
    {
        var indent = new string(' ', depth * 2);
        var icon = node.IsLeaf ? "📄" : "📁";
        
        Console.WriteLine($"{indent}{icon} {node.Name}");
        
        if (verbose)
        {
            Console.WriteLine($"{indent}   ID: {node.Id}");
            Console.WriteLine($"{indent}   Level: {node.Level}");
            Console.WriteLine($"{indent}    Components: {node.Components.Count}");
            Console.WriteLine($"{indent}   Est. Tokens: {node.EstimatedTokens:N0}");
            Console.WriteLine($"{indent}   Complexity: {node.ComplexityScore:F2}");
            
            if (node.QualityMetrics != null)
            {
                Console.WriteLine($"{indent}   Metrics:");
                Console.WriteLine($"{indent}     - Cohesion: {node.QualityMetrics.Cohesion:F3}");
                Console.WriteLine($"{indent}     - Coupling: {node.QualityMetrics.Coupling:F3}");
                Console.WriteLine($"{indent}     - Instability: {node.QualityMetrics.Instability:F3}");
                Console.WriteLine($"{indent}     - Abstractness: {node.QualityMetrics.Abstractness:F3}");
                Console.WriteLine($"{indent}     - Distance: {node.QualityMetrics.DistanceFromMainSequence:F3}");
            }

            if (node.Metadata.Any())
            {
                Console.WriteLine($"{indent}   Metadata:");
                foreach (var kvp in node.Metadata)
                {
                    Console.WriteLine($"{indent}     - {kvp.Key}: {kvp.Value}");
                }
            }
        }

        foreach (var child in node.Children)
        {
            PrintNode(child, depth + 1, verbose);
        }
    }

    public static async Task SaveToJsonAsync(ModuleTree tree, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(tree, options);
        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"\n✓ Module tree saved to: {outputPath}");
    }

    public static void PrintStatistics(ModuleTree tree)
    {
        var leaves = tree.GetAllLeaves();
        var maxDepth = tree.Nodes.Values.Max(n => n.Level);
        var totalTokens = leaves.Sum(n => n.EstimatedTokens);
        var avgTokens = leaves.Any() ? leaves.Average(n => n.EstimatedTokens) : 0;
        var avgCohesion = leaves.Where(n => n.QualityMetrics != null).Any() 
            ? leaves.Where(n => n.QualityMetrics != null).Average(n => n.QualityMetrics.Cohesion)
            : 0;
        var avgCoupling = leaves.Where(n => n.QualityMetrics != null).Any()
            ? leaves.Where(n => n.QualityMetrics != null).Average(n => n.QualityMetrics.Coupling) 
            : 0;

        Console.WriteLine("\n=== Statistics ===");
        Console.WriteLine($"Total Modules: {tree.Nodes.Count}");
        Console.WriteLine($"Leaf Modules: {leaves.Count}");
        Console.WriteLine($"Max Depth: {maxDepth}");
        Console.WriteLine($"Total Est. Tokens: {totalTokens:N0}");
        Console.WriteLine($"Avg Tokens/Module: {avgTokens:N0}");
        Console.WriteLine($"Avg Cohesion: {avgCohesion:F3}");
        Console.WriteLine($"Avg Coupling: {avgCoupling:F3}");
    }
}
