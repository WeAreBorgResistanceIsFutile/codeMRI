using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class DocumentationSynthesisService : IDocumentationSynthesisService
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<DocumentationSynthesisService> _logger;
    private readonly IHierarchicalSummaryService? _summaryService;

    public DocumentationSynthesisService(
        ILLMClient llmClient, 
        ILogger<DocumentationSynthesisService> logger,
        IHierarchicalSummaryService? summaryService = null)
    {
        _llmClient = llmClient;
        _logger = logger;
        _summaryService = summaryService;
    }

    public async Task<WikiPage> SynthesizeParentPageAsync(ModuleNode module, List<WikiPage> childPages,
        string language = "English", AudienceType audience = AudienceType.Developer, bool mergeChildContent = false)
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
            
            string simpleContent;
            int simpleThreshold = (int)(_llmClient.ContextSize * 3.5);
            if (simplePrompt.Length > simpleThreshold)
            {
                simpleContent = await _llmClient.ChatWithFindingsAsync(
                    "You are a technical documentation expert.",
                    "Summarize the following module information.",
                    simplePrompt);
            }
            else
            {
                simpleContent = await _llmClient.ChatAsync(
                    "You are a technical documentation expert.", 
                    simplePrompt,
                    new List<ChatMessage>());
            }
            
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = module.Name,
                Content = CleanContent(simpleContent, module.Name),
                RelevantFiles = new List<string>()
            };
        }

            // Check if we're merging cluster content
        if (mergeChildContent)
        {
            _logger.LogInformation("Merging {Count} cluster pages into parent page for {ModuleName}", 
                childPages.Count, module.Name);
            
            var mergePrompt = PromptTemplates.MergeClusterPagesPrompt(
                module,
                childPages,
                language);
            
            string mergedContent;
            int mergeThreshold = (int)(_llmClient.ContextSize * 3.5);
            if (mergePrompt.Length > mergeThreshold)
            {
                _logger.LogInformation("Merge prompt too large ({Length}). Using findings-based synthesis.", mergePrompt.Length);
                mergedContent = await _llmClient.ChatWithFindingsAsync(
                    "You are a technical documentation expert specializing in content synthesis and organization.",
                    "Merge the following documentation clusters into a single cohesive document as instructed.",
                    mergePrompt);
            }
            else
            {
                mergedContent = await _llmClient.ChatAsync(
                    "You are a technical documentation expert specializing in content synthesis and organization.",
                    mergePrompt,
                    new List<ChatMessage>());
            }
            
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = module.Name,
                Content = CleanContent(mergedContent, module.Name),
                RelevantFiles = childPages.SelectMany(p => p.RelevantFiles ?? new List<string>()).Distinct().ToList(),
                Metadata = new Dictionary<string, object>
                {
                    { "IsMergedFromClusters", true },
                    { "ClusterCount", childPages.Count }
                }
            };
        }
    
        // Otherwise, perform standard parent page synthesis
        // Estimate cross-module dependencies (simplified - could be enhanced with graph data)
        var estimatedCrossModuleDeps = childPages.Count > 1 ? childPages.Count * 2 : 0;

        // Entity Anchoring: Extract key entities from child pages
        ExtractedEntities? entities = null;
        if (_summaryService != null && childPages.Count > 0)
        {
            entities = _summaryService.ExtractKeyEntities(childPages);
            if (entities.HasEntities)
            {
                _logger.LogInformation(
                    "Extracted {Count} anchored entities from {ChildCount} child pages for {ModuleName}",
                    entities.AllEntities.Count(), childPages.Count, module.Name);
            }
        }

        // Determine synthesis strategy and final prompt
        string prompt;
        int overviewThreshold = (int)(_llmClient.ContextSize * 3.5);
        bool useMapReduce = false;
        
        // Initial prompt estimation (Direct or Entity Anchored)
        string basePrompt = entities?.HasEntities == true
            ? PromptTemplates.EntityAnchoredSynthesisPrompt(module, childPages, entities, estimatedCrossModuleDeps, language)
            : audience == AudienceType.All
                ? PromptTemplates.ComprehensiveParentPageSynthesisPrompt(module, childPages, estimatedCrossModuleDeps, language)
                : audience != AudienceType.Developer
                    ? PromptTemplates.UserGuideSynthesisPrompt(module, childPages, audience, language)
                    : PromptTemplates.ParentPageSynthesisPrompt(module, childPages, estimatedCrossModuleDeps, language);

        if (basePrompt.Length > overviewThreshold && _summaryService != null)
        {
            _logger.LogInformation("Synthesis prompt for {ModuleName} exceeds threshold ({Length}). Initiating Map-Reduce.", module.Name, basePrompt.Length);
            
            // MAP Phase: Summarize each child page
            var summaries = new List<ModuleSummary>();
            foreach (var childPage in childPages)
            {
                var summary = await _summaryService.SummarizeModuleAsync(childPage, 200, default);
                summaries.Add(summary);
            }
            
            // REDUCE Phase: Synthesize from summaries
            prompt = PromptTemplates.MapReduceSynthesisPrompt(
                module,
                summaries,
                entities ?? new ExtractedEntities(),
                estimatedCrossModuleDeps,
                language);
            
            useMapReduce = true;
            _logger.LogDebug("Using Map-Reduce synthesis for {ModuleName}", module.Name);
        }
        else
        {
            prompt = basePrompt;
            _logger.LogDebug("Using {Strategy} synthesis for {ModuleName}", 
                entities?.HasEntities == true ? "Entity-Anchored" : "Direct", module.Name);
        }

        string overviewContent;
        if (prompt.Length > overviewThreshold)
        {
            _logger.LogWarning("Final synthesis prompt still exceeds threshold ({Length}) for {ModuleName}. Using findings-based synthesis as last resort.", prompt.Length, module.Name);
            overviewContent = await _llmClient.ChatWithFindingsAsync(
                "You are a technical documentation expert.",
                "Synthesize architectural documentation from the following child module data.",
                prompt);
        }
        else
        {
            overviewContent = await _llmClient.ChatAsync(
                "You are a technical documentation expert.", 
                prompt,
                new List<ChatMessage>());
        }

        var resultPage = new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = module.Name,
            Content = CleanContent(overviewContent, module.Name),
            RelevantFiles = new List<string>()
        };
        
        if (useMapReduce)
        {
            resultPage.Metadata["SynthesisStrategy"] = "MapReduce";
            resultPage.Metadata["ChildSummaryCount"] = childPages.Count;
        }
        else if (entities?.HasEntities == true)
        {
            resultPage.Metadata["SynthesisStrategy"] = "WithEntityAnchoring";
            resultPage.Metadata["AnchoredEntityCount"] = entities.AllEntities.Count();
        }
        else
        {
            resultPage.Metadata["SynthesisStrategy"] = "Direct";
        }
        
        return resultPage;
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