using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class RubricGenerationService : IRubricGenerationService
{
    private readonly ILLMServiceFacade _llmFacade;
    private readonly ILogger<RubricGenerationService> _logger;
    private readonly IJsonRepairService _jsonRepairService;

    public RubricGenerationService(
        ILogger<RubricGenerationService> logger,
        ILLMServiceFacade llmFacade,
        IJsonRepairService jsonRepairService)
    {
        _logger = logger;
        _llmFacade = llmFacade;
        _jsonRepairService = jsonRepairService;
    }

    public async Task<EvaluationRubric> GenerateRubricAsync(
        WikiStructure documentationStructure,
        RepositoryInfo repositoryInfo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating evaluation rubric for repository: {RepositoryName}", repositoryInfo.Name);

        var prompt = BuildRubricGenerationPrompt(documentationStructure, repositoryInfo);

        var llmResponse = await _llmFacade.ExecuteAsync(
            systemPrompt: "You are a documentation evaluation assistant.",
            textToProcess: prompt,
            history: null,
            options: null,
            cancellationToken: cancellationToken);
        
        var response = llmResponse.Content;

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
            $@"To enhance this prompt, we need to solve the ""Granularity Problem."" Asking an AI to generate 40–60 leaf nodes often results in repetitive or generic requirements (e.g., ""Check if X is documented,"" ""Check if Y is documented"").

This enhanced version uses **Layered Analysis**. It forces the AI to look at the repo through four specific lenses: **Foundational**, **Functional**, **Technical**, and **Operational**.

---

## Enhanced Evaluator Prompt

**Role**: You are a Lead Technical Documentation Auditor and Information Architect. Your task is to generate a rigorous, quantitative evaluation rubric for a codebase's documentation based on its specific structure and scale.

**Input Context**:
* **Repository**: {repositoryInfo.Name} ({repositoryInfo.Language})
* **Scale**: {repositoryInfo.LinesOfCode} LOC / {repositoryInfo.ComponentCount} Components
* **Navigation Structure**:
{structureJson}
---

### Phase 1: Rubric Architecture (Weights & Categories)

You must distribute the **1.0 total weight** across these four mandatory top-level categories. Adjust the weights slightly (±0.05) if the project scale or language suggests a different priority:

1. **Architecture & High-Level Design (0.25)**: Evaluates mental models, component interactions, and data flow.
2. **Feature & Functional Coverage (0.30)**: Evaluates how well the ""What"" and ""Why"" of each module in the `structureJson` is explained.
3. **Technical & API Reference (0.25)**: Evaluates the precision of interfaces, types, and parameters.
4. **Operational & Developer Experience (0.20)**: Evaluates setup, examples, error handling, and contribution workflows.

---

### Phase 2: Leaf Node Generation Rules (The ""Specific & Measurable"" Standard)

Each leaf node description must avoid vague words like ""good"" or ""thorough."" Instead, use **Verification Markers**:

* ""Verify the presence of a sequence diagram for...""
* ""Check for a table defining all environment variables in...""
* ""Confirm that every public method in [Module Name] includes a code snippet.""
* ""Evaluate if the documentation explains the failure modes of...""

---

### Phase 3: Structural Requirements

* **Quantity**: Generate exactly **40–60 leaf nodes**.
* **Distribution**: Distribute leaf nodes proportionally across the modules listed in the `structureJson`.
* **Math Check**: Ensure that `weight` sums to exactly **1.0** at the root level and exactly **1.0** for the children of any given node.
* **Leaf Flag**: Only nodes with `""is_leaf"": true` should contain a `description`.

---

### Output Format

Return **ONLY** valid JSON. Do not include markdown blocks or preamble.

```json
{{
    ""title"": ""{{{{repositoryInfo.Name}}}} Documentation Evaluation Rubric"",
    ""weight"": 1.0,
    ""children"": [
        {{
            ""title"": ""Category Name"",
            ""weight"": 0.25,
            ""children"": [
                {{
                    ""title"": ""Specific Requirement Title"",
                    ""weight"": 0.1,
                    ""is_leaf"": true,
                    ""description"": ""Verification instruction using specific markers.""
                }}
            ]
        }}
    ]
}}

```
";
    }

    private EvaluationRubric ParseRubricFromResponse(string response)
    {
        try
        {
            // Use JsonRepairService to extract and clean JSON
            var cleanResponse = _jsonRepairService.ExtractJsonString(response);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            // Using a local helper to pre-process JSON and add $type discriminators if missing, 
            // allowing us to use built-in polymorphic deserialization without a custom converter.
            using (var doc = JsonDocument.Parse(cleanResponse))
            {
                var root = doc.RootElement;
                var enrichedJson = EnrichJsonWithDiscriminators(root);
                
                var rubric = JsonSerializer.Deserialize<EvaluationRubric>(enrichedJson, options);

                if (rubric == null)
                {
                    _logger.LogWarning("Failed to parse rubric from LLM response");
                    return CreateDefaultRubric();
                }

                return rubric;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing rubric JSON from LLM response");
            return CreateDefaultRubric();
        }
    }

    private string EnrichJsonWithDiscriminators(JsonElement element)
    {
        // Simple recursive enrichment to add $type based on content if missing
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                EnrichRecursive(writer, element);
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private void EnrichRecursive(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            
            bool hasType = false;
            bool isLeaf = false;
            bool isEvaluationRubric = false;

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("$type")) hasType = true;
                if (prop.Name.ToLowerInvariant().Replace("_", "") == "isleaf" && prop.Value.ValueKind == JsonValueKind.True) isLeaf = true;
                // Heuristic: root object usually has repository info or specific title
                if (prop.Name.ToLowerInvariant() == "title" && prop.Value.GetString()?.Contains("Rubric") == true) isEvaluationRubric = true;
            }

            if (!hasType)
            {
                if (isLeaf) writer.WriteString("$type", "requirement");
                else if (isEvaluationRubric) writer.WriteString("$type", "rubric");
                else writer.WriteString("$type", "category");
            }

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.ToLowerInvariant() == "children")
                {
                    writer.WritePropertyName("children");
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        writer.WriteStartArray();
                        foreach (var item in prop.Value.EnumerateArray()) EnrichRecursive(writer, item);
                        writer.WriteEndArray();
                    }
                    else writer.WriteNullValue();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) EnrichRecursive(writer, item);
            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
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