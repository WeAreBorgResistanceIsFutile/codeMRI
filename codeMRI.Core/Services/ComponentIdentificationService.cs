using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services;

public class ComponentIdentificationService : IComponentIdentificationService
{
    private readonly ILogger<ComponentIdentificationService> _logger;
    private readonly RoslynCSharpAnalyzer _roslynAnalyzer;
    private readonly Dictionary<string, string> _languagePatterns = new()
    {
        { ".cs", "C#" },
        { ".java", "Java" },
        { ".py", "Python" },
        { ".js", "JavaScript" },
        { ".ts", "TypeScript" },
        { ".cpp", "C++" },
        { ".c", "C" },
        { ".go", "Go" },
        { ".rs", "Rust" }
    };

    public ComponentIdentificationService(ILogger<ComponentIdentificationService> logger)
    {
        _logger = logger;
        _roslynAnalyzer = new RoslynCSharpAnalyzer();
    }

    public async Task<RepositoryStructure> AnalyzeRepositoryAsync(string repositoryPath)
    {
        var structure = new RepositoryStructure
        {
            Name = Path.GetFileName(repositoryPath),
            Path = repositoryPath
        };

        if (!Directory.Exists(repositoryPath))
        {
            _logger.LogWarning("Repository path does not exist: {Path}", repositoryPath);
            return structure;
        }

        var files = Directory.GetFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !IsIgnoredPath(f))
            .ToList();

        structure.Files = files.Select(f => Path.GetRelativePath(repositoryPath, f)).ToList();
        structure.Directories = Directory.GetDirectories(repositoryPath, "*", SearchOption.AllDirectories)
            .Where(d => !IsIgnoredPath(d))
            .Select(d => Path.GetRelativePath(repositoryPath, d)).ToList();

        structure.FileExtensions = files
            .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
            .Where(g => _languagePatterns.ContainsKey(g.Key))
            .ToDictionary(g => g.Key, g => g.Count());

        structure.Language = structure.FileExtensions.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key switch
        {
            ".cs" => "C#",
            ".java" => "Java",
            ".py" => "Python",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            _ => "Mixed"
        };

        return await Task.FromResult(structure);
    }

    public async Task<List<CodeComponent>> IdentifyComponentsAsync(string repositoryPath)
    {
        var components = new List<CodeComponent>();
        var files = Directory.GetFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsSourceFile(f) && !IsIgnoredPath(f))
            .ToList();

        foreach (var file in files)
        {
            var fileComponents = await AnalyzeFileAsync(file);
            components.AddRange(fileComponents);
        }

        return components;
    }

    public async Task<ComponentRelationships> AnalyzeRelationshipsAsync(string repositoryPath, List<CodeComponent> components)
    {
        var relationships = new ComponentRelationships();
        var componentMap = components.ToDictionary(c => c.Id, c => c);

        foreach (var component in components)
        {
            await AnalyzeComponentDependencies(component, relationships, componentMap);
        }

        return relationships;
    }

    private async Task<List<CodeComponent>> AnalyzeFileAsync(string filePath)
    {
        var components = new List<CodeComponent>();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        if (!_languagePatterns.ContainsKey(extension))
            return components;

        var content = await File.ReadAllTextAsync(filePath);
        var language = _languagePatterns[extension];

        switch (language)
        {
            case "C#":
                components.AddRange(await _roslynAnalyzer.AnalyzeCSharpFileAsync(filePath));
                break;
            case "Java":
                components.AddRange(AnalyzeJavaFile(filePath, content));
                break;
            case "Python":
                components.AddRange(AnalyzePythonFile(filePath, content));
                break;
        }

        return components;
    }



    private List<CodeComponent> AnalyzeJavaFile(string filePath, string content)
    {
        var components = new List<CodeComponent>();
        var lines = content.Split('\n');
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var componentCounter = 1;
        
        var classPattern = @"(?:public|private|protected)?\s*(?:abstract|final|static)?\s*class\s+(\w+)";
        var interfacePattern = @"(?:public|private|protected)?\s*interface\s+(\w+)";

        foreach (Match match in Regex.Matches(content, classPattern))
        {
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{match.Groups[1].Value}_{componentCounter++}",
                Name = match.Groups[1].Value,
                Type = "Class",
                FilePath = filePath,
                Language = "Java",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content)
            });
        }

        foreach (Match match in Regex.Matches(content, interfacePattern))
        {
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{match.Groups[1].Value}_{componentCounter++}",
                Name = match.Groups[1].Value,
                Type = "Interface",
                FilePath = filePath,
                Language = "Java",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content)
            });
        }

        return components;
    }

    private List<CodeComponent> AnalyzePythonFile(string filePath, string content)
    {
        var components = new List<CodeComponent>();
        var lines = content.Split('\n');
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var componentCounter = 1;
        
        var classPattern = @"^class\s+(\w+):";

        foreach (Match match in Regex.Matches(content, classPattern, RegexOptions.Multiline))
        {
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{match.Groups[1].Value}_{componentCounter++}",
                Name = match.Groups[1].Value,
                Type = "Class",
                FilePath = filePath,
                Language = "Python",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content)
            });
        }

        return components;
    }

    private async Task AnalyzeComponentDependencies(CodeComponent component, ComponentRelationships relationships, Dictionary<string, CodeComponent> componentMap)
    {
        if (!File.Exists(component.FilePath))
            return;

        var content = await File.ReadAllTextAsync(component.FilePath);
        
        // Improved dependency analysis - look for type references with word boundaries
        foreach (var otherComponent in componentMap.Values)
        {
            if (otherComponent.Id == component.Id) continue;

            // Use word boundary regex to avoid partial matches
            var pattern = $@"\b{Regex.Escape(otherComponent.Name)}\b";
            var matches = Regex.Matches(content, pattern);
            
            if (matches.Count > 0)
            {
                relationships.Dependencies.Add(new Dependency
                {
                    FromComponent = component.Id,
                    ToComponent = otherComponent.Id,
                    Type = "TypeReference",
                    Strength = Math.Min(matches.Count, 10) // Cap strength at 10
                });
            }
        }
    }

    private int CalculateComplexity(string content)
    {
        var complexity = 1;
        
        // Add complexity for control structures
        complexity += Regex.Matches(content, @"\b(if|else|while|for|switch|case|catch|throw)\b").Count;
        
        // Add complexity for nested structures (only opening braces)
        complexity += Regex.Matches(content, @"\{").Count;
        
        return Math.Max(1, Math.Min(complexity, 10)); // Ensure minimum of 1, cap at 10
    }



    private bool IsSourceFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return _languagePatterns.ContainsKey(extension);
    }

    private bool IsIgnoredPath(string path)
    {
        var relativePath = Path.GetFileName(path);
        var ignoredDirectories = new[] { "bin", "obj", "node_modules", ".git", ".vs", "dist", "build" };
        var ignoredExtensions = new[] { ".dll", ".exe", ".pdb", ".cache", ".tmp" };

        return ignoredDirectories.Any(dir => path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar)) ||
               ignoredExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }
}