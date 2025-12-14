using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class DocumentationSynthesisService : IDocumentationSynthesisService
{
    private readonly ILLMClient _llmClient;

    public DocumentationSynthesisService(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<WikiPage> SynthesizeParentPageAsync(ModuleNode module, List<WikiPage> childPages,
        string language = "English")
    {
        // If no children, perform a simple generation
        if (childPages == null || !childPages.Any())
        {
            var simplePrompt = $"""
                Generate a brief overview for the module '{module.Name}'. It has no child modules detected.
                
                Module Context:
                - Level: {module.Level}
                - Architectural Pattern: {module.Metadata.GetValueOrDefault("ArchitecturalPattern", "Not identified")}
                - Description: {module.Description ?? "N/A"}
                
                Quality Metrics:
                - Cohesion: {module.QualityMetrics.Cohesion:F2}
                - Coupling: {module.QualityMetrics.Coupling:F2}
                - Complexity: {module.ComplexityScore:F1}
                
                STRICT RULES:
                - Do NOT invent features or components
                - Use {language} for all content
                
                Output markdown starting with # {module.Name}
                """;
            
            var simpleContent = await _llmClient.ChatAsync(
                "You are a technical documentation expert.", 
                simplePrompt,
                new List<ChatMessage>());
            
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = module.Name,
                Content = CleanContent(simpleContent, module.Name),
                RelevantFiles = new List<string>()
            };
        }

        // Estimate cross-module dependencies (simplified - could be enhanced with graph data)
        var estimatedCrossModuleDeps = childPages.Count > 1 ? childPages.Count * 2 : 0;

        // Use the new ParentPageSynthesisPrompt from PromptTemplates
        var prompt = PromptTemplates.ParentPageSynthesisPrompt(
            module,
            childPages,
            estimatedCrossModuleDeps,
            language);

        var overviewContent = await _llmClient.ChatAsync(
            "You are a technical documentation expert and software architect.", 
            prompt,
            new List<ChatMessage>());

        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = module.Name,
            Content = CleanContent(overviewContent, module.Name),
            RelevantFiles = new List<string>()
        };
    }

    /// <summary>
    /// Cleans the LLM output to ensure consistent formatting.
    /// </summary>
    private string CleanContent(string content, string title)
    {
        if (string.IsNullOrWhiteSpace(content))
            return $"# {title}\n\n*Documentation pending.*";
        
        var cleaned = content.Trim();
        
        // Remove markdown code fences if the LLM wrapped the entire output
        if (cleaned.StartsWith("```markdown"))
        {
            cleaned = cleaned.Substring("```markdown".Length).TrimStart('\n', '\r');
        }
        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3).TrimEnd();
        }
        
        // Ensure it starts with the title
        if (!cleaned.StartsWith($"# {title}"))
        {
            // Remove any existing title and add the canonical one
            if (cleaned.StartsWith("# "))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline > 0)
                {
                    cleaned = cleaned.Substring(firstNewline).TrimStart('\n', '\r');
                }
            }
            cleaned = $"# {title}\n\n{cleaned}";
        }
        
        return cleaned;
    }
}