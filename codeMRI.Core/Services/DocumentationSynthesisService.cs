using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services;

public class DocumentationSynthesisService : IDocumentationSynthesisService
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<DocumentationSynthesisService> _logger;
    private readonly CodeWikiOptions _options;
    private readonly IHierarchicalSummaryService? _summaryService;

    public DocumentationSynthesisService(
        ILLMClient llmClient,
        ILogger<DocumentationSynthesisService> logger,
        IOptions<CodeWikiOptions> options,
        IHierarchicalSummaryService? summaryService = null)
    {
        _llmClient = llmClient;
        _logger = logger;
        _options = options.Value;
        _summaryService = summaryService;
    }

    public async Task<WikiPage> SynthesizeParentPageAsync(ModuleNode module, List<WikiPage> childPages,
        string language = "English", AudienceType audience = AudienceType.Developer, bool mergeChildContent = false,
        SynthesisStrategy? strategy = null, CancellationToken cancellationToken = default)
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
            var simpleThreshold = (int)(_llmClient.ContextSize * 3.5);
            if (simplePrompt.Length > simpleThreshold)
                simpleContent = await _llmClient.ChatWithFindingsAsync(
                    "You are a master software architect generating high-quality documentation.",
                    "Summarize the following module information.",
                    simplePrompt,
                    null,
                    cancellationToken,
                    _options.UseSemanticChunking);
            else
                simpleContent = await _llmClient.ChatAsync(
                    "You are a technical documentation expert.",
                    simplePrompt,
                    new List<ChatMessage>());

            var simplePageTitle = GetFriendlyPageTitle(module.Name);
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = simplePageTitle,
                Content = CleanContent(simpleContent, simplePageTitle),
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
            var mergeThreshold = (int)(_llmClient.ContextSize * 3.5);
            if (mergePrompt.Length > mergeThreshold)
            {
                _logger.LogInformation("Merge prompt too large ({Length}). Using findings-based synthesis.",
                    mergePrompt.Length);
                mergedContent = await _llmClient.ChatWithFindingsAsync(
                    "You are a master software architect generating high-quality documentation.",
                    "Merge the following documentation clusters into a single cohesive document as instructed.",
                    mergePrompt,
                    null,
                    cancellationToken,
                    _options.UseSemanticChunking);
            }
            else
            {
                mergedContent = await _llmClient.ChatAsync(
                    "You are a technical documentation expert specializing in content synthesis and organization.",
                    mergePrompt,
                    new List<ChatMessage>());
            }

            var mergedPageTitle = GetFriendlyPageTitle(module.Name);
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = mergedPageTitle,
                Content = CleanContent(mergedContent, mergedPageTitle),
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
                _logger.LogInformation(
                    "Extracted {Count} anchored entities from {ChildCount} child pages for {ModuleName}",
                    entities.AllEntities.Count(), childPages.Count, module.Name);
        }

        // Determine synthesis strategy and final prompt
        string prompt;
        var overviewThreshold = (int)(_llmClient.ContextSize * 3.5);
        var useMapReduce = false;

        var activeStrategy = strategy ?? _options.DefaultSynthesisStrategy;

        // Initial prompt estimation (Direct or Entity Anchored)
        var basePrompt = activeStrategy == SynthesisStrategy.ChainOfKey
            ? PromptTemplates.ChainOfKeySynthesisPrompt(module, childPages, entities ?? new ExtractedEntities(),
                estimatedCrossModuleDeps, language)
            : entities?.HasEntities == true && activeStrategy != SynthesisStrategy.Direct
                ? PromptTemplates.EntityAnchoredSynthesisPrompt(module, childPages, entities, estimatedCrossModuleDeps,
                    language)
                : audience != AudienceType.Developer
                    ? PromptTemplates.TesterGuideSynthesisPrompt(module, childPages, audience, language)
                    : PromptTemplates.ParentPageSynthesisPrompt(module, childPages, estimatedCrossModuleDeps,
                        language);

        if ((basePrompt.Length > overviewThreshold || activeStrategy == SynthesisStrategy.MapReduce) &&
            _summaryService != null)
        {
            _logger.LogInformation("Synthesis prompt for {ModuleName} {Reason}. Initiating Map-Reduce.",
                module.Name,
                activeStrategy == SynthesisStrategy.MapReduce
                    ? "requested via strategy"
                    : $"exceeds threshold ({basePrompt.Length})");

            // MAP Phase: Summarize each child page
            var summaries = new List<ModuleSummary>();
            foreach (var childPage in childPages)
            {
                var summary = await _summaryService.SummarizeModuleAsync(childPage);
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
            _logger.LogWarning(
                "Final synthesis prompt still exceeds threshold ({Length}) for {ModuleName}. Using findings-based synthesis as last resort.",
                prompt.Length, module.Name);
            overviewContent = await _llmClient.ChatWithFindingsAsync(
                "You are a master software architect generating high-quality documentation.",
                "Synthesize architectural documentation from the following child module data.",
                prompt,
                null,
                cancellationToken,
                _options.UseSemanticChunking);
        }
        else
        {
            overviewContent = await _llmClient.ChatAsync(
                "You are a master software architect generating high-quality documentation.",
                prompt,
                new List<ChatMessage>(),
                null,
                cancellationToken);
        }

        var finalContent = overviewContent;
        var metadata = new Dictionary<string, object>
        {
            { "SynthesisStrategy", activeStrategy.ToString() }
        };

        if (activeStrategy == SynthesisStrategy.ChainOfKey)
            try
            {
                var jsonStart = overviewContent.IndexOf('{');
                var jsonEnd = overviewContent.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = overviewContent.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var state = JsonSerializer.Deserialize<JsonElement>(json);
                    if (state.TryGetProperty("narrativeMarkdown", out var markdownProp))
                    {
                        finalContent = markdownProp.GetString() ?? overviewContent;

                        // Capture other keys into metadata
                        foreach (var prop in state.EnumerateObject())
                            if (prop.Name != "narrativeMarkdown")
                                metadata[$"ChainOfKey_{prop.Name}"] = prop.Value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Chain-of-Key JSON for {ModuleName}. Using raw response.",
                    module.Name);
            }

        var friendlyTitle = GetFriendlyPageTitle(module.Name);
        var resultPage = new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = friendlyTitle,
            Content = CleanContent(finalContent, friendlyTitle),
            RelevantFiles = childPages.SelectMany(p => p.RelevantFiles ?? new List<string>()).Distinct().ToList(),
            Metadata = metadata
        };

        if (useMapReduce)
            resultPage.Metadata["MapReduce_ChildSummaryCount"] = childPages.Count;
        else if (entities?.HasEntities == true && activeStrategy != SynthesisStrategy.Direct)
            resultPage.Metadata["AnchoredEntityCount"] = entities.AllEntities.Count();

        return resultPage;
    }

    /// <summary>
    ///     Cleans the LLM output to ensure consistent formatting.
    /// </summary>
    private string CleanContent(string content, string title)
    {
        if (string.IsNullOrWhiteSpace(content))
            return $"# {title}\n\n*Documentation pending.*";

        var cleaned = content.Trim();

        // Remove markdown code fences if the LLM wrapped the entire output
        if (cleaned.StartsWith("```markdown")) cleaned = cleaned.Substring("```markdown".Length).TrimStart('\n', '\r');
        if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3).TrimEnd();

        // Ensure it starts with the title
        if (!cleaned.StartsWith($"# {title}"))
        {
            // Remove any existing title and add the canonical one
            if (cleaned.StartsWith("# "))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline > 0) cleaned = cleaned.Substring(firstNewline).TrimStart('\n', '\r');
            }

            cleaned = $"# {title}\n\n{cleaned}";
        }

        return cleaned;
    }

    /// <summary>
    ///     Transforms a module name into a human-friendly page title
   /// </summary>
    private string GetFriendlyPageTitle(string moduleName)
    {
        var title = moduleName;

        // Remove path prefixes like "Src/Main/Java" or "Src Main Java"
        var pathPrefixes = new[] 
        { 
            "Src Main Java", "Src Test Java", "Src Main", "Src Test",
            "Src/Main/Java", "Src/Test/Java", "Src/Main", "Src/Test",
            "Src/", "src/", "Main/", "Test/"
        };
        
        foreach (var prefix in pathPrefixes)
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                title = title.Substring(prefix.Length).Trim();
                break;
            }
        }

        // Convert path separators to spaces for readability
        title = title.Replace('/', ' ').Replace('\\', ' ');

        // Transform common technical terms to user-friendly names
        var technicalToFriendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "CrossCutting", "Shared Utilities" },
            { "Repository", "Data Access" },
            { "Infrastructure", "System Infrastructure" },
            { "Presentation", "User Interface" },
            { "Domain", "Business Logic" },
            { "Application", "Application Services" },
            { "Data", "Data Layer" }
        };

        // Check if the title matches a technical term exactly
        if (technicalToFriendly.TryGetValue(title.Trim(), out var friendlyName))
        {
            return friendlyName;
        }

        // Clean up multiple spaces
        while (title.Contains("  "))
            title = title.Replace("  ", " ");

        return title.Trim();
    }
}