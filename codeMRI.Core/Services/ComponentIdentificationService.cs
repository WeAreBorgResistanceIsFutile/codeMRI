using System.Text.RegularExpressions;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class ComponentIdentificationService : IComponentIdentificationService
{
    private readonly IASTServiceClient? _astServiceClient;

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

    private readonly ILogger<ComponentIdentificationService> _logger;
    private readonly RoslynCSharpAnalyzer _roslynAnalyzer;

    public ComponentIdentificationService(
        ILogger<ComponentIdentificationService> logger,
        IASTServiceClient? astServiceClient = null)
    {
        _logger = logger;
        _roslynAnalyzer = new RoslynCSharpAnalyzer();
        _astServiceClient = astServiceClient;
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

    public async Task<ComponentRelationships> AnalyzeRelationshipsAsync(string repositoryPath,
        List<CodeComponent> components)
    {
        var relationships = new ComponentRelationships();
        var componentMap = components.ToDictionary(c => c.Id, c => c);

        foreach (var component in components)
            await AnalyzeComponentDependencies(component, relationships, componentMap);

        // Identify entry points (zero in-degree components)
        relationships.EntryPoints = IdentifyEntryPoints(components, relationships);

        return relationships;
    }

    private List<string> IdentifyEntryPoints(List<CodeComponent> components, ComponentRelationships relationships)
    {
        var componentIds = components.Select(c => c.Id).ToHashSet();
        var dependentComponents = relationships.Dependencies
            .Select(d => d.ToComponent)
            .ToHashSet();

        // Entry points are components that are not depended on by other components
        var entryPoints = componentIds
            .Except(dependentComponents)
            .ToList();

        // Filter for likely entry points based on naming and type
        return entryPoints.Where(id =>
        {
            var component = components.FirstOrDefault(c => c.Id == id);
            return component != null && IsLikelyEntryPoint(component);
        }).ToList();
    }

    private bool IsLikelyEntryPoint(CodeComponent component)
    {
        var entryPointPatterns = new[]
        {
            "main", "program", "startup", "entry", "init",
            "controller", "service", "api", "handler"
        };

        var name = component.Name.ToLowerInvariant();

        // Check naming patterns
        if (entryPointPatterns.Any(pattern => name.Contains(pattern)))
            return true;

        // Check component types
        if (component.Type.Equals("Controller", StringComparison.OrdinalIgnoreCase) ||
            component.Type.Equals("Service", StringComparison.OrdinalIgnoreCase) ||
            component.Type.Equals("API", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for public methods that could be entry points
        if (component.Metadata.IsPublic && component.Metadata.FanIn == 0)
            return true;

        return false;
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
            case "Python":
            case "JavaScript":
            case "TypeScript":
            case "C++":
            case "C":
                // Try to use AST Service if available
                if (_astServiceClient != null && await _astServiceClient.IsHealthyAsync())
                    try
                    {
                        var astResult = await _astServiceClient.ParseCodeAsync(content, language, filePath);
                        if (astResult != null)
                        {
                            var astComponents = await _astServiceClient.ConvertToCodeComponentsAsync(astResult);
                            components.AddRange(astComponents);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AST Service failed to parse {FilePath}, falling back to basic analysis",
                            filePath);
                    }

                // Fallback to basic analysis
                if (language == "Java")
                    components.AddRange(AnalyzeJavaFile(filePath, content));
                else if (language == "Python")
                    components.AddRange(AnalyzePythonFile(filePath, content));
                else
                    components.AddRange(AnalyzeGenericFile(filePath, content));
                break;
            default:
                // Basic regex-based analysis for unsupported languages
                components.AddRange(AnalyzeGenericFile(filePath, content));
                break;
        }

        return components;
    }

    private List<CodeComponent> AnalyzeGenericFile(string filePath, string content)
    {
        var components = new List<CodeComponent>();
        var lines = content.Split('\n');
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var componentCounter = 1;

        // Generic class/function detection for various languages
        var classPatterns = new[]
        {
            @"\bclass\s+(\w+)",
            @"\binterface\s+(\w+)",
            @"\bstruct\s+(\w+)",
            @"\benum\s+(\w+)"
        };

        var functionPatterns = new[]
        {
            @"\bfunction\s+(\w+)",
            @"\bdef\s+(\w+)",
            @"\bfunc\s+(\w+)",
            @"\bpublic\s+\w+\s+(\w+)\s*\(",
            @"\bprivate\s+\w+\s+(\w+)\s*\("
        };

        foreach (var pattern in classPatterns)
        foreach (Match match in Regex.Matches(content, pattern, RegexOptions.IgnoreCase))
        {
            var componentName = match.Groups[1].Value;
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{componentName}_{componentCounter++}",
                Name = componentName,
                Type = "Class",
                FilePath = filePath,
                Language = "Unknown",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content),
                Metadata = CreateComponentMetadata(filePath, content, componentName)
            });
        }

        foreach (var pattern in functionPatterns)
        foreach (Match match in Regex.Matches(content, pattern, RegexOptions.IgnoreCase))
        {
            var componentName = match.Groups[1].Value;
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{componentName}_{componentCounter++}",
                Name = componentName,
                Type = "Function",
                FilePath = filePath,
                Language = "Unknown",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content),
                Metadata = CreateComponentMetadata(filePath, content, componentName)
            });
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
            var componentName = match.Groups[1].Value;
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{componentName}_{componentCounter++}",
                Name = componentName,
                Type = "Class",
                FilePath = filePath,
                Language = "Java",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content),
                Metadata = CreateComponentMetadata(filePath, content, componentName)
            });
        }

        foreach (Match match in Regex.Matches(content, interfacePattern))
        {
            var componentName = match.Groups[1].Value;
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{componentName}_{componentCounter++}",
                Name = componentName,
                Type = "Interface",
                FilePath = filePath,
                Language = "Java",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content),
                Metadata = CreateComponentMetadata(filePath, content, componentName)
            });
        }

        foreach (Match match in Regex.Matches(content, interfacePattern))
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
            var componentName = match.Groups[1].Value;
            components.Add(new CodeComponent
            {
                Id = $"{fileName}_{componentName}_{componentCounter++}",
                Name = componentName,
                Type = "Class",
                FilePath = filePath,
                Language = "Python",
                LineCount = lines.Length,
                ComplexityScore = CalculateComplexity(content),
                Metadata = CreateComponentMetadata(filePath, content, componentName)
            });
        }

        return components;
    }

    private async Task AnalyzeComponentDependencies(CodeComponent component, ComponentRelationships relationships,
        Dictionary<string, CodeComponent> componentMap)
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
                relationships.Dependencies.Add(new Dependency
                {
                    FromComponent = component.Id,
                    ToComponent = otherComponent.Id,
                    Type = "TypeReference",
                    Strength = Math.Min(matches.Count, 10) // Cap strength at 10
                });
        }
    }

    private int CalculateComplexity(string content)
    {
        return ComputeCyclomaticComplexity(content);
    }

    private int ComputeCyclomaticComplexity(string content)
    {
        var complexity = 1; // Base complexity

        // Decision points that increase cyclomatic complexity
        var decisionPatterns = new[]
        {
            @"\bif\s*\(", // if statements
            @"\belse\s+if\s*\(", // else if statements  
            @"\bwhile\s*\(", // while loops
            @"\bfor\s*\(", // for loops
            @"\bforeach\s*\(", // foreach loops
            @"\bswitch\s*\(", // switch statements
            @"\bcase\s+", // case statements
            @"\bcatch\s*\(", // catch blocks
            @"\b\?\s*", // ternary operator
            @"\|\|", // logical OR
            @"&&" // logical AND
        };

        foreach (var pattern in decisionPatterns) complexity += Regex.Matches(content, pattern).Count;

        return Math.Max(1, complexity);
    }

    private int CalculateNestingDepth(string content)
    {
        var maxDepth = 0;
        var currentDepth = 0;
        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Count opening braces/keywords that increase depth
            currentDepth += Regex.Matches(trimmedLine, @"\{").Count;
            currentDepth += Regex.Matches(trimmedLine, @"\b(if|while|for|foreach|switch|using|lock)\s*\(").Count;

            // Count closing braces that decrease depth
            currentDepth -= Regex.Matches(trimmedLine, @"\}").Count;

            maxDepth = Math.Max(maxDepth, currentDepth);
        }

        return Math.Max(0, maxDepth);
    }

    private ComponentMetadata CreateComponentMetadata(string filePath, string content, string componentName)
    {
        var lines = content.Split('\n');
        var cyclomaticComplexity = ComputeCyclomaticComplexity(content);
        var nestingDepth = CalculateNestingDepth(content);
        var loc = lines.Length;

        // Calculate fan-in/fan-out (simplified version)
        var fanIn = CountIncomingReferences(content, componentName);
        var fanOut = CountOutgoingReferences(content);

        return new ComponentMetadata
        {
            Loc = loc,
            CyclomaticComplexity = cyclomaticComplexity,
            NestingDepth = nestingDepth,
            FanIn = fanIn,
            FanOut = fanOut,
            IsPublic = IsPublicComponent(content),
            DocstringPresent = HasDocumentation(content),
            EstimatedTokens = EstimateTokens(loc, cyclomaticComplexity)
        };
    }

    private int CountIncomingReferences(string content, string componentName)
    {
        // Simplified: count potential external references this component could receive
        // In a real implementation, this would analyze the entire codebase
        return IsPublicComponent(content) ? 1 : 0;
    }

    private int CountOutgoingReferences(string content)
    {
        // Count external dependencies (imports, using statements, etc.)
        var patterns = new[]
        {
            @"using\s+[\w.]+;",
            @"import\s+[\w.]+;",
            @"#include\s+[<""].+[>""]",
            @"require\s*\(",
            @"from\s+[\w.]+\s+import"
        };

        return patterns.Sum(pattern => Regex.Matches(content, pattern).Count);
    }

    private bool IsPublicComponent(string content)
    {
        var publicPatterns = new[]
        {
            @"\bpublic\s+(class|interface|struct|enum)",
            @"\bexport\s+(class|interface|function|const)",
            @"__all__\s*=",
            @"@app\.route",
            @"@RestController",
            @"@Controller",
            @"@Service",
            @"@Component"
        };

        return publicPatterns.Any(pattern => Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
    }

    private bool HasDocumentation(string content)
    {
        var docPatterns = new[]
        {
            @"/\*\*[\s\S]*?\*/", // JSDoc/JavaDoc style
            @"///.*", // XML documentation comments
            @"""[\s\S]*?""", // Python docstrings (triple quotes)
            @"#.*", // Python comments
            @"//.*" // Single line comments
        };

        return docPatterns.Any(pattern => Regex.IsMatch(content, pattern));
    }

    private string DetermineComponentType(string content)
    {
        if (Regex.IsMatch(content, @"\bclass\s+\w+")) return "Class";
        if (Regex.IsMatch(content, @"\binterface\s+\w+")) return "Interface";
        if (Regex.IsMatch(content, @"\benum\s+\w+")) return "Enum";
        if (Regex.IsMatch(content, @"\bstruct\s+\w+")) return "Struct";
        if (Regex.IsMatch(content, @"\bfunction\s+\w+|\bdef\s+\w+")) return "Function";
        if (Regex.IsMatch(content, @"@\w+Controller|@RestController")) return "Controller";
        if (Regex.IsMatch(content, @"@\w+Service")) return "Service";

        return "Component";
    }

    private double EstimateTokens(int loc, int complexity)
    {
        // Rough estimation: ~1.3 tokens per line of code + complexity overhead
        var baseTokens = loc * 1.3;
        var complexityOverhead = complexity * 10;
        return baseTokens + complexityOverhead;
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

        return ignoredDirectories.Any(dir =>
                   path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar)) ||
               ignoredExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }
}