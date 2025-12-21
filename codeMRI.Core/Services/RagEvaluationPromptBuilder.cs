using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class RagEvaluationPromptBuilder : IEvaluationPromptBuilder
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<RagEvaluationPromptBuilder> _logger;

    public RagEvaluationPromptBuilder(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILogger<RagEvaluationPromptBuilder> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
    }

    public virtual async Task<string> BuildPromptAsync(
        RubricRequirement requirement, 
        WikiStructure structure)
    {
        var repoPath = structure.RepoPath;
        // 1. Embed the requirement description
        var query = $"Requirement: {requirement.Title}. Description: {requirement.Description}";
        var vector = await _embeddingService.GetEmbeddingAsync(query);

        // 2. Search for relevant documentation chunks
        var collectionName = "documentation";
        var filters = new Dictionary<string, object> { { "repoPath", repoPath } };
        var results = await _vectorStoreService.SearchAsync(collectionName, vector, topK: 5, filters);

        // 3. Construct prompt with retrieved context
        var context = string.Join("\n\n---\n\n", results.Select(r => r.Document.Text));

        return $@"
### TASK: Documentation Evaluation
Evaluate the documentation of the repository against the following requirement.

### REQUIREMENT TO EVALUATE:
Name: {requirement.Title}
Description: {requirement.Description}
Weight: {requirement.Weight}

### RELEVANT DOCUMENTATION CONTEXT:
The following sections from the generated documentation were retrieved as most relevant to this requirement:

{context}

---

### LIGHTWEIGHT DOCUMENTATION STRUCTURE:
{FormatDocumentationSkeleton(structure)}

### INSTRUCTIONS:
1. Carefully analyze the provided context and the overall structure.
2. Determine to what extent the requirement is met.
3. Provide a score from 0.0 to 1.0.
4. Provide a detailed justification, citing specific sections if possible.

### OUTPUT FORMAT:
Return a JSON object with the following fields:
- score: (float)
- justification: (string)
";
    }

    private string FormatDocumentationSkeleton(WikiStructure structure)
    {
        var skeleton = new
        {
            structure.Title,
            structure.Description,
            Pages = structure.Pages.Select(p => new { p.Id, p.Title, p.Description })
        };
        return JsonSerializer.Serialize(skeleton, new JsonSerializerOptions { WriteIndented = true });
    }
}
