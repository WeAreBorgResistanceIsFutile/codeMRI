using System.Text.Json;
using codeMRI.Core.Converters;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class RubricGenerationService : IRubricGenerationService
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<RubricGenerationService> _logger;

    public RubricGenerationService(
        ILogger<RubricGenerationService> logger,
        ILLMClient llmClient)
    {
        _logger = logger;
        _llmClient = llmClient;
    }

    public async Task<EvaluationRubric> GenerateRubricAsync(
        WikiStructure documentationStructure,
        RepositoryInfo repositoryInfo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating evaluation rubric for repository: {RepositoryName}", repositoryInfo.Name);

        var prompt = BuildRubricGenerationPrompt(documentationStructure, repositoryInfo);

        var response = await _llmClient.ChatAsync("You are a documentation evaluation assistant.", prompt,
            new List<ChatMessage>(),
            null,
            cancellationToken);

        var rubric = ParseRubricFromResponse(response);

        _logger.LogInformation("Generated rubric with {RequirementCount} requirements",
            CountLeafRequirements(rubric));

        return rubric;
    }

    public async Task<EvaluationRubric> GenerateConsensusRubricAsync(
        WikiStructure documentationStructure,
        RepositoryInfo repositoryInfo,
        List<string> modelNames,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating consensus rubric using {ModelCount} models", modelNames.Count);

        var rubrics = new List<EvaluationRubric>();

        // Generate rubric with each model
        foreach (var modelName in modelNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var modelRubric = await GenerateRubricWithModelAsync(
                    documentationStructure, repositoryInfo, modelName, cancellationToken);

                if (modelRubric != null) rubrics.Add(modelRubric);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate rubric with model: {ModelName}", modelName);
            }
        }

        // Synthesize consensus
        var consensusRubric = await SynthesizeRubricsAsync(rubrics, cancellationToken);

        _logger.LogInformation("Consensus rubric generated with {RequirementCount} requirements",
            CountLeafRequirements(consensusRubric));

        return consensusRubric;
    }

    private async Task<EvaluationRubric?> GenerateRubricWithModelAsync(
        WikiStructure documentationStructure,
        RepositoryInfo repositoryInfo,
        string modelName,
        CancellationToken cancellationToken)
    {
        // This would integrate with different LLM clients
        // For now, use the default client
        return await GenerateRubricAsync(documentationStructure, repositoryInfo, cancellationToken);
    }

    private async Task<EvaluationRubric> SynthesizeRubricsAsync(
        List<EvaluationRubric> rubrics,
        CancellationToken cancellationToken)
    {
        var allRequirements = new List<RubricRequirement>();

        // Extract all requirements from all rubrics
        foreach (var rubric in rubrics)
        {
            var requirements = ExtractLeafRequirements(rubric);
            allRequirements.AddRange(requirements);
        }

        // Deduplicate requirements using semantic similarity
        var uniqueRequirements = await DeduplicateRequirementsAsync(allRequirements, cancellationToken);

        // Rebuild hierarchical structure
        var consensusRubric = new EvaluationRubric
        {
            Title = "Consensus Evaluation Rubric",
            Weight = 1.0,
            Children = OrganizeRequirementsHierarchy(uniqueRequirements)
        };

        return consensusRubric;
    }

    private async Task<List<RubricRequirement>> DeduplicateRequirementsAsync(
        List<RubricRequirement> requirements,
        CancellationToken cancellationToken)
    {
        // Simple deduplication based on text similarity
        var uniqueRequirements = new List<RubricRequirement>();
        var seenTexts = new HashSet<string>();

        foreach (var requirement in requirements)
        {
            var normalizedText = NormalizeRequirementText(requirement.Description);

            if (!seenTexts.Contains(normalizedText))
            {
                seenTexts.Add(normalizedText);
                uniqueRequirements.Add(requirement);
            }
        }

        return await Task.FromResult(uniqueRequirements);
    }

    private string NormalizeRequirementText(string text)
    {
        return text.ToLowerInvariant()
            .Replace(".", "")
            .Replace(",", "")
            .Replace(";", "")
            .Trim();
    }

    private List<RubricRequirement> ExtractLeafRequirements(EvaluationRubric rubric)
    {
        var requirements = new List<RubricRequirement>();

        void ExtractRecursive(RubricNode node)
        {
            if (node is RubricRequirement requirement && node.IsLeaf)
                requirements.Add(requirement);
            else if (node.Children != null)
                foreach (var child in node.Children)
                    ExtractRecursive(child);
        }

        ExtractRecursive(rubric);
        return requirements;
    }

    private List<RubricNode> OrganizeRequirementsHierarchy(List<RubricRequirement> requirements)
    {
        // Simple organization by category
        var categories = new Dictionary<string, List<RubricRequirement>>
        {
            ["Documentation Quality"] = new(),
            ["API Coverage"] = new(),
            ["Code Examples"] = new(),
            ["Structure & Organization"] = new()
        };

        // Categorize requirements
        foreach (var requirement in requirements)
        {
            var category = CategorizeRequirement(requirement);
            categories[category].Add(requirement);
        }

        // Build hierarchy
        var children = new List<RubricNode>();

        foreach (var category in categories)
            if (category.Value.Any())
                children.Add(new RubricCategory
                {
                    Title = category.Key,
                    Weight = 0.25, // Equal weight for now
                    Children = category.Value.Cast<RubricNode>().ToList()
                });

        return children;
    }

    private string CategorizeRequirement(RubricRequirement requirement)
    {
        var text = requirement.Description.ToLowerInvariant();

        if (text.Contains("example") || text.Contains("code") || text.Contains("sample"))
            return "Code Examples";

        if (text.Contains("api") || text.Contains("endpoint") || text.Contains("method"))
            return "API Coverage";

        if (text.Contains("structure") || text.Contains("organization") || text.Contains("format"))
            return "Structure & Organization";

        return "Documentation Quality";
    }

    private string BuildRubricGenerationPrompt(WikiStructure documentationStructure, RepositoryInfo repositoryInfo)
    {
        var structureJson =
            JsonSerializer.Serialize(documentationStructure, new JsonSerializerOptions { WriteIndented = true });

        return
            $@"You are a technical documentation evaluator. Given the repository information and documentation structure, 
generate a comprehensive evaluation rubric that captures:

1. Core architectural components
2. Key features and capabilities  
3. API/interface specifications
4. Usage patterns and examples

Repository Information:
- Name: {repositoryInfo.Name}
- Language: {repositoryInfo.Language}
- Lines of Code: {repositoryInfo.LinesOfCode}
- Components: {repositoryInfo.ComponentCount}

Documentation Structure:
{structureJson}

Generate a hierarchical evaluation rubric with the following JSON structure:
{{
    ""title"": ""Repository Name Documentation Evaluation"",
    ""weight"": 1.0,
    ""children"": [
        {{
            ""title"": ""Component Category"",
            ""weight"": 0.25,
            ""children"": [
                {{
                    ""title"": ""Specific Requirement"",
                    ""weight"": 0.5,
                    ""is_leaf"": true,
                    ""description"": ""What should be documented""
                }}
            ]
        }}
    ]
}}

Requirements:
- Generate 40-60 total leaf requirements
- Leaf nodes should be specific and measurable
- Weights should sum to 1.0 at each level
- Focus on documentation completeness and accuracy
- Include requirements for code examples, API documentation, and architectural overview

Return only valid JSON.";
    }

    private EvaluationRubric ParseRubricFromResponse(string response)
    {
        try
        {
            var cleanResponse = CleanJsonString(response);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new RubricNodeConverter());

            var rubric = JsonSerializer.Deserialize<EvaluationRubric>(cleanResponse, options);

            if (rubric == null)
            {
                _logger.LogWarning("Failed to parse rubric from LLM response");
                return CreateDefaultRubric();
            }

            return rubric;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing rubric JSON from LLM response");
            return CreateDefaultRubric();
        }
    }

    private string CleanJsonString(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return response;

        var cleaned = response.Trim();
        
        // Remove markdown code blocks if present
        if (cleaned.Contains("```json"))
        {
            var start = cleaned.IndexOf("```json") + 7;
            var end = cleaned.LastIndexOf("```");
            if (end > start)
            {
                cleaned = cleaned.Substring(start, end - start);
            }
        }
        else if (cleaned.StartsWith("```"))
        {
            var firstLineBreak = cleaned.IndexOf('\n');
            var lastBackticks = cleaned.LastIndexOf("```");
            if (firstLineBreak > 0 && lastBackticks > firstLineBreak)
            {
                cleaned = cleaned.Substring(firstLineBreak + 1, lastBackticks - firstLineBreak - 1);
            }
        }
        
        // Sometimes LLMs add text before or after the JSON even without code blocks
        // Find the first '{' and last '}'
        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        
        return cleaned.Trim();
    }

    private EvaluationRubric CreateDefaultRubric()
    {
        return new EvaluationRubric
        {
            Title = "Default Evaluation Rubric",
            Weight = 1.0,
            Children = new List<RubricNode>
            {
                new RubricCategory
                {
                    Title = "Documentation Coverage",
                    Weight = 0.4,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "Component Documentation",
                            Weight = 0.5,
                            IsLeaf = true,
                            Description = "All major components are documented"
                        },
                        new RubricRequirement
                        {
                            Title = "API Documentation",
                            Weight = 0.5,
                            IsLeaf = true,
                            Description = "Public APIs are fully documented"
                        }
                    }
                },
                new RubricCategory
                {
                    Title = "Documentation Quality",
                    Weight = 0.3,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "Code Examples",
                            Weight = 0.5,
                            IsLeaf = true,
                            Description = "Documentation includes practical code examples"
                        }
                    }
                },
                new RubricCategory
                {
                    Title = "Structure & Organization",
                    Weight = 0.3,
                    Children = new List<RubricNode>
                    {
                        new RubricRequirement
                        {
                            Title = "Navigation",
                            Weight = 0.5,
                            IsLeaf = true,
                            Description = "Documentation is well-organized and navigable"
                        }
                    }
                }
            }
        };
    }

    private int CountLeafRequirements(EvaluationRubric rubric)
    {
        var count = 0;

        void CountRecursive(RubricNode node)
        {
            if (node is RubricRequirement requirement && node.IsLeaf)
                count++;
            else if (node.Children != null)
                foreach (var child in node.Children)
                    CountRecursive(child);
        }

        CountRecursive(rubric);
        return count;
    }
}