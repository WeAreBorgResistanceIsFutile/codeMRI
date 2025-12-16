using System.Text;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;


namespace codeMRI.Core.Services;

public class WikiGenerationService : IWikiGenerationService
{
    private readonly ILogger<WikiGenerationService> _logger;
    private readonly IDiagramGenerator _diagramGenerator;
    private readonly IEnhancedDependencyGraphService _graphService; // Needed to fetch graph for diagrams
    private readonly ILLMClient _llmClient;
    private readonly IDocumentationSynthesisService _synthesisService;
    private readonly IReferenceManagementService _referenceManagementService;
    private readonly string _documentationModel;

    public WikiGenerationService(
        ILLMClient llmClient,
        IDiagramGenerator diagramGenerator,
        IEnhancedDependencyGraphService graphService,
        IDocumentationSynthesisService synthesisService,
        IReferenceManagementService referenceManagementService, ILogger<WikiGenerationService> logger, string documentationModel)
    {
        _llmClient = llmClient;
        _diagramGenerator = diagramGenerator;
        _graphService = graphService;
        _synthesisService = synthesisService;
        _referenceManagementService = referenceManagementService;
        _logger = logger;
        _documentationModel = documentationModel;
    }

    public async Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths,
        Dictionary<string, string> fileContents, string language = "English", string? repoPath = null)
    {
        // If no files provided, try to find them using the dependency graph (CodeWiki structural approach)
        if (filePaths == null || filePaths.Count == 0)
        {
            try 
            {
                // Optimization: Set a strict timeout for graph generation
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                EnhancedDependencyGraph graph;
                if (!string.IsNullOrEmpty(repoPath))
                {
                    // 1. Get components from repository
                    var components = await _graphService.GetComponentsAsync(repoPath, cts.Token);
                    // 2. Build graph to understand relationships
                    graph = await _graphService.BuildGraphAsync(components, cts.Token);
                }
                else
                {
                    // Fallback (mostly for tests without repo access)
                    graph = await _graphService.BuildGraphAsync(new List<CodeComponent>(), cts.Token);
                }

                // 3. Find likely entry point matching the page title
                var entryPointId = FindEntryPointForPage(pageTitle, new List<string>(), graph);

                if (!string.IsNullOrEmpty(entryPointId))
                {
                    var node = graph.GetNode(entryPointId);
                    if (node != null && !string.IsNullOrEmpty(node.Metadata?.FilePath))
                    {
                        var path = node.Metadata.FilePath;
                        filePaths = new List<string> { path };

                        // 4. Load content if accessible or empty (force reload if empty)
                        if (!fileContents.ContainsKey(path) || string.IsNullOrWhiteSpace(fileContents[path]))
                        {
                            if (File.Exists(path))
                            {
                                fileContents[path] = await File.ReadAllTextAsync(path, cts.Token);
                            }
                            else if (!string.IsNullOrEmpty(repoPath))
                            {
                                var fullPath = Path.Combine(repoPath, path);
                                if (File.Exists(fullPath))
                                {
                                    fileContents[path] = await File.ReadAllTextAsync(fullPath, cts.Token);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving file for page '{PageTitle}': {Message}", pageTitle, ex.Message);
                // Fail gracefully, generation will likely be generic
                filePaths = new List<string>();
            }
        }
        else
        {
             // If files provided but contents missing, try to load them
             foreach(var path in filePaths)
             {
                 if (!fileContents.ContainsKey(path) || string.IsNullOrWhiteSpace(fileContents[path]))
                 {
                      try 
                      {
                           // Check absolute or repo-relative path
                           if (File.Exists(path))
                           {
                               fileContents[path] = await File.ReadAllTextAsync(path);
                           }
                           else if (!string.IsNullOrEmpty(repoPath))
                           {
                               var fullPath = Path.Combine(repoPath, path);
                               if (File.Exists(fullPath))
                               {
                                   fileContents[path] = await File.ReadAllTextAsync(fullPath);
                               }
                           }
                      }
                      catch (Exception ex)
                      {
                           _logger.LogWarning(ex, "Warning: Could not read file content for {Path}: {Message}", path, ex.Message);
                      }
                 }
             }
         }

        // Validate that we have actual content before proceeding
        // This prevents hallucinated content generation when no files are available
        if (filePaths == null || filePaths.Count == 0 || 
            !fileContents.Any(kv => !string.IsNullOrWhiteSpace(kv.Value)))
        {
            _logger.LogWarning("Warning: No content available for page '{PageTitle}'. Skipping detailed generation.", pageTitle);
            return new WikiPage 
            { 
                Id = Guid.NewGuid().ToString(),
                Title = pageTitle,
                Content = $"# {pageTitle}\n\n*Documentation pending - no source files available.*",
                RelevantFiles = new List<string>()
            };
        }

        var contextBuilder = new StringBuilder();
        foreach (var path in filePaths)
            if (fileContents.ContainsKey(path))
            {
                contextBuilder.AppendLine($"File: {path}");
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine(fileContents[path]);
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine();
            }

        var prompt = PromptTemplates.PagePrompt(pageTitle, filePaths, language);
        var fullPrompt = prompt + "\n\nSOURCE FILES CONTENT:\n" + contextBuilder;

        var content = await _llmClient.ChatAsync("", fullPrompt, new List<ChatMessage>(), _documentationModel);

        // Generate enhanced interactive diagrams if we have a dependency graph
        try
        {
            // Optimization: Set a strict timeout for graph generation to avoid hanging page loads
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var graph = await _graphService.BuildGraphAsync(new List<CodeComponent>(), cts.Token);
            if (graph.NodeCount > 0)
            {
                // Try to find a component that matches the page title or file paths
                var entryPointId = FindEntryPointForPage(pageTitle, filePaths, graph);

                if (!string.IsNullOrEmpty(entryPointId))
                {
                    // Generate interactive sequence diagram
                    if (_diagramGenerator is IDiagramGenerator enhancedGenerator)
                    {
                        var interactiveSequenceDiagram =
                            await enhancedGenerator.GenerateInteractiveSequenceDiagramAsync(graph, entryPointId);
                        if (interactiveSequenceDiagram != null &&
                            !string.IsNullOrWhiteSpace(interactiveSequenceDiagram.MermaidContent))
                        {
                            content += "\n\n## Interactive Sequence Diagram\n\n";
                            content += GenerateInteractiveDiagramHtml(interactiveSequenceDiagram, "Sequence Diagram");
                        }
                    }
                    else
                    {
                        // Fallback to basic diagram generation
                        var sequenceDiagram = await _diagramGenerator.GenerateSequenceDiagramAsync(graph, entryPointId);
                        if (!string.IsNullOrWhiteSpace(sequenceDiagram) && sequenceDiagram.Contains("sequenceDiagram"))
                        {
                            content += "\n\n## Sequence Diagram\n\n";
                            content += "```mermaid\n" + sequenceDiagram + "\n```\n";
                        }
                    }

                    // Generate interactive component diagram
                    if (_diagramGenerator is IDiagramGenerator enhancedComponentGenerator)
                    {
                        var interactiveComponentDiagram =
                            await enhancedComponentGenerator.GenerateInteractiveComponentDiagramAsync(graph,
                                entryPointId);
                        if (interactiveComponentDiagram != null &&
                            !string.IsNullOrWhiteSpace(interactiveComponentDiagram.MermaidContent))
                        {
                            content += "\n\n## Interactive Component Diagram\n\n";
                            content += GenerateInteractiveDiagramHtml(interactiveComponentDiagram, "Component Diagram");
                        }
                    }
                    else
                    {
                        // Fallback to basic diagram generation
                        var componentDiagram =
                            await _diagramGenerator.GenerateComponentDiagramAsync(graph, entryPointId);
                        if (!string.IsNullOrWhiteSpace(componentDiagram) && componentDiagram.Contains("classDiagram"))
                        {
                            content += "\n## Component Diagram\n\n";
                            content += "```mermaid\n" + componentDiagram + "\n```\n";
                        }
                    }

                    // Generate Data Flow Diagram
                    var dataFlowDiagram = await _diagramGenerator.GenerateDataFlowDiagramAsync(graph, entryPointId);
                    if (!string.IsNullOrWhiteSpace(dataFlowDiagram) && dataFlowDiagram.Contains("graph"))
                    {
                        content += "\n\n## Data Flow Diagram\n\n";
                        content += "```mermaid\n" + dataFlowDiagram + "\n```\n";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the page generation
            // In a real implementation, you'd use proper logging
            _logger.LogError(ex, "Failed to generate diagrams for page {PageTitle}: {Message}", pageTitle, ex.Message);
        }

        // Enrich content with intelligent cross-links
        // Use pageTitle as sourceComponentId context if possible, or a safe fallback
        var pageId = Guid.NewGuid().ToString();
        content = _referenceManagementService.EnrichContentWithLinks(content, pageTitle); // Using title as ID proxy for now
        content = CleanLLMPageContent(content, pageTitle, filePaths);

        return new WikiPage
        {
            Id = pageId,
            Title = pageTitle,
            Content = content,
            RelevantFiles = filePaths
        };
    }

    /// <summary>
    /// Post-processes the LLM-generated content to remove unwanted preambles and ensure
    /// the relevant files <details> block is at the very end.
    /// </summary>
    private string CleanLLMPageContent(string llmContent, string pageTitle, List<string> filePaths)
    {
        var cleanedContent = llmContent.Trim();

        // 0. Strip markdown code fences that LLMs sometimes wrap around the entire output
        // This fixes the issue where ```markdown appears at the start and ``` at the end
        if (cleanedContent.StartsWith("```markdown"))
        {
            // Remove opening fence (```markdown) and any immediate newlines
            cleanedContent = cleanedContent.Substring("```markdown".Length).TrimStart('\n', '\r');
        }
        
        // Remove closing fence if present at the end
        if (cleanedContent.EndsWith("```"))
        {
            cleanedContent = cleanedContent.Substring(0, cleanedContent.Length - 3).TrimEnd();
        }
        
        cleanedContent = cleanedContent.Trim();

        // 1. Remove all existing <details> blocks related to source files
        // Using a regex that captures the specific "Relevant source files" summary to avoid removing other details blocks
        var detailsPattern = @"<details>\s*<summary>\s*Relevant source files\s*<\/summary>.*?<\/details>";
        cleanedContent = System.Text.RegularExpressions.Regex.Replace(cleanedContent, detailsPattern, "", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline).Trim();

        // 2. Remove all main title headers (H1) that resemble the page title
        // This handles "# Title", "#Title", " # Title" etc.
        var titlePattern = @"^\s*#\s*" + System.Text.RegularExpressions.Regex.Escape(pageTitle) + @"\s*$";
        cleanedContent = System.Text.RegularExpressions.Regex.Replace(cleanedContent, titlePattern, "", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline).Trim();
        
        // Also remove the raw ID/Title if it appears as a standalone line at the very beginning (common LLM artifact)
        var rawTitlePattern = @"^\s*" + System.Text.RegularExpressions.Regex.Escape(pageTitle) + @"\s*$";
        cleanedContent = System.Text.RegularExpressions.Regex.Replace(cleanedContent, rawTitlePattern, "",
             System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline).Trim();

        // 3. Construct the final clean content
        var sb = new StringBuilder();
        
        // Add canonical Title
        sb.AppendLine($"# {pageTitle}");
        sb.AppendLine();
        
        // Add content
        sb.Append(cleanedContent);
        
        // Add canonical Details block
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("<details>");
        sb.AppendLine("<summary>Relevant source files</summary>");
        sb.AppendLine();
        foreach(var path in filePaths)
        {
            sb.AppendLine($"- {path}");
        }
        sb.AppendLine("</details>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a wiki page using the enhanced prompt with full module context.
    /// This provides richer documentation by including quality metrics, dependencies, and related pages.
    /// </summary>
    public async Task<WikiPage> GenerateEnhancedPageAsync(
        ModuleNode module,
        List<WikiPage>? relatedPages,
        ModulePageContext context,
        Dictionary<string, string> fileContents,
        string language = "English",
        string? repoPath = null,
        AudienceType audience = AudienceType.Developer)
    {
        var filePaths = module.Components.ToList();
        
        // Ensure we have file contents for all components
        foreach (var componentId in module.Components)
        {
            if (!fileContents.ContainsKey(componentId) || string.IsNullOrWhiteSpace(fileContents[componentId]))
            {
                try
                {
                    if (File.Exists(componentId))
                    {
                        fileContents[componentId] = await File.ReadAllTextAsync(componentId);
                    }
                    else if (!string.IsNullOrEmpty(repoPath))
                    {
                        var fullPath = Path.Combine(repoPath, componentId.TrimStart('/', '\\'));
                        if (File.Exists(fullPath))
                        {
                            fileContents[componentId] = await File.ReadAllTextAsync(fullPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read file content for component {ComponentId}", componentId);
                }
            }
        }

        // Validate content availability
        var availableFiles = fileContents.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToList();
        if (!availableFiles.Any())
        {
            _logger.LogWarning("No content available for module '{ModuleName}'. Generating placeholder.", module.Name);
            return new WikiPage
            {
                Id = Guid.NewGuid().ToString(),
                Title = module.Name,
                Content = $"# {module.Name}\n\n*Documentation pending - no source files available.*",
                RelevantFiles = filePaths
            };
        }

        // Build source files context
        var contextBuilder = new StringBuilder();
        foreach (var kv in availableFiles)
        {
            contextBuilder.AppendLine($"File: {kv.Key}");
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine(kv.Value);
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine();
        }

        // Use the enhanced prompt with module context based on audience
        string prompt;
        if (audience != AudienceType.Developer)
        {
             // For User/DevOps, use the specific prompt template
             // Note: availableFiles contains the source content we want to pass
             var sourceContent = availableFiles.ToDictionary(k => k.Key, v => v.Value);
             prompt = PromptTemplates.UserGuidePagePrompt(module, context, sourceContent, audience, language);
        }
        else
        {
             prompt = PromptTemplates.EnhancedPagePrompt(module, relatedPages, context, language);
        }
        var fullPrompt = prompt + "\n\nSOURCE FILES CONTENT:\n" + contextBuilder;

        var content = await _llmClient.ChatAsync("", fullPrompt, new List<ChatMessage>(), _documentationModel);

        // Generate diagrams if available and audience is Developer
        if (audience == AudienceType.Developer)
        {
            try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var graph = await _graphService.BuildGraphAsync(new List<CodeComponent>(), cts.Token);
            
            if (graph.NodeCount > 0)
            {
                var entryPointId = FindEntryPointForPage(module.Name, filePaths, graph);
                if (!string.IsNullOrEmpty(entryPointId))
                {
                    // Add component diagram
                    var componentDiagram = await _diagramGenerator.GenerateComponentDiagramAsync(graph, entryPointId);
                    if (!string.IsNullOrWhiteSpace(componentDiagram) && componentDiagram.Contains("classDiagram"))
                    {
                        content += "\n\n## Component Diagram\n\n";
                        content += "```mermaid\n" + componentDiagram + "\n```\n";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate diagrams for module {ModuleName}: {Message}", module.Name, ex.Message);
        }
        }

        // Enrich with cross-links and clean up
        content = _referenceManagementService.EnrichContentWithLinks(content, module.Name);
        content = CleanLLMPageContent(content, module.Name, filePaths);

        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = module.Name,
            Content = content,
            RelevantFiles = filePaths
        };
    }

    public async Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages,
        string language = "English", AudienceType audience = AudienceType.Developer)
    {

        // 1. Delegate synthesis to the specialized service
        var page = await _synthesisService.SynthesizeParentPageAsync(module, childPages, language, audience);
        var content = page.Content;

        // 2. Generate Architecture Diagram (Developer only)
        if (audience == AudienceType.Developer)
        {
            try
        {
            var graph = await _graphService.BuildGraphAsync(new List<CodeComponent>(), CancellationToken.None);
            if (graph.NodeCount > 0)
            {
                // Create a simple module tree for this module and its children
                var moduleTree = new ModuleTree { Root = module };
                moduleTree.Nodes[module.Id] = module;

                var architectureDiagram = await _diagramGenerator.GenerateArchitectureDiagramAsync(moduleTree, graph);
                if (!string.IsNullOrWhiteSpace(architectureDiagram) && architectureDiagram.Contains("graph T"))
                {
                    content += "\n\n## Architecture Diagram\n\n";
                    content += "```mermaid\n" + architectureDiagram + "\n```\n";
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the page generation
            _logger.LogError(ex, "Failed to generate architecture diagram for module {ModuleName}: {Message}", module.Name, ex.Message);
            }
        }

        // Enrich with links
        content = _referenceManagementService.EnrichContentWithLinks(content, module.Id);

        page.Content = content;
        return page;
    }

    private string ExtractSummary(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        var idx = content.IndexOf("\n\n");
        if (idx > 0) return content.Substring(0, idx);
        return content.Length > 200 ? content.Substring(0, 200) + "..." : content;
    }

    private string? FindEntryPointForPage(string pageTitle, List<string> filePaths, EnhancedDependencyGraph graph)
    {
        // Try to find a component that matches the page title
        var titleCandidates = graph.GetNodes()
            .Where(n => n.ComponentId.Equals(pageTitle, StringComparison.OrdinalIgnoreCase) ||
                        n.ComponentId.Contains(pageTitle, StringComparison.OrdinalIgnoreCase) ||
                        pageTitle.Contains(n.ComponentId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (titleCandidates.Any()) return titleCandidates.First().ComponentId;

        // Try to match based on file paths
        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var fileCandidates = graph.GetNodes()
                .Where(n => n.ComponentId.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                            n.ComponentId.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
                            fileName.Contains(n.ComponentId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (fileCandidates.Any()) return fileCandidates.First().ComponentId;
        }

        // If no specific match, return the first node with outgoing edges (likely an entry point)
        var entryPoint = graph.GetNodes()
            .Where(n => n.OutDegree > 0)
            .OrderByDescending(n => n.OutDegree)
            .FirstOrDefault();

        return entryPoint?.ComponentId;
    }

    /// <summary>
    ///     Generate HTML wrapper for interactive diagrams with zoom, filter, and export controls
    /// </summary>
    private string GenerateInteractiveDiagramHtml(InteractiveDiagram diagram, string title)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"<div class=\"interactive-diagram-container\" data-diagram-id=\"{diagram.Id}\">");
        sb.AppendLine($"<h3>{title}</h3>");
        
        // Control Panel
        sb.AppendLine("<div class=\"diagram-controls\">");
        // Zoom Controls
        sb.AppendLine("<div class=\"zoom-controls\">");
        sb.AppendLine($"<button class=\"btn btn-sm btn-outline-secondary\" onclick=\"zoomIn('{diagram.Id}')\">+</button>");
        sb.AppendLine($"<button class=\"btn btn-sm btn-outline-secondary\" onclick=\"zoomOut('{diagram.Id}')\">-</button>");
        sb.AppendLine($"<button class=\"btn btn-sm btn-outline-secondary\" onclick=\"resetZoom('{diagram.Id}')\">Reset</button>");
        sb.AppendLine($"<button class=\"btn btn-sm btn-outline-secondary\" onclick=\"fitToView('{diagram.Id}')\">Fit</button>");
        sb.AppendLine("</div>");
        
        // Filter Controls
        sb.AppendLine("<div class=\"filter-controls\">");
        sb.AppendLine($"<select class=\"form-select form-select-sm\" id=\"filter-type-{diagram.Id}\" onchange=\"applyFilters('{diagram.Id}')\">");
        sb.AppendLine("<option value=\"\">All Types</option>");
        sb.AppendLine($"{GenerateComponentTypeOptions(diagram.Components)}");
        sb.AppendLine("</select>");
        
        sb.AppendLine($"<select class=\"form-select form-select-sm\" id=\"filter-layer-{diagram.Id}\" onchange=\"applyFilters('{diagram.Id}')\">");
        sb.AppendLine("<option value=\"\">All Layers</option>");
        sb.AppendLine($"{GenerateLayerOptions(diagram.Components)}");
        sb.AppendLine("</select>");
        
        sb.AppendLine($"<input type=\"range\" class=\"form-range\" id=\"filter-complexity-{diagram.Id}\" ");
        sb.AppendLine($"min=\"0\" max=\"100\" value=\"100\" onchange=\"applyFilters('{diagram.Id}')\"");
        sb.AppendLine("title=\"Filter by complexity\">");
        sb.AppendLine("</div>");
        
        // Export Controls
        sb.AppendLine("<div class=\"export-controls\">");
        sb.AppendLine($"<button class=\"btn btn-sm btn-primary\" onclick=\"exportDiagram('{diagram.Id}', 'png')\">Export PNG</button>");
        sb.AppendLine($"<button class=\"btn btn-sm btn-primary\" onclick=\"exportDiagram('{diagram.Id}', 'svg')\">Export SVG</button>");
        sb.AppendLine($"<button class=\"btn btn-sm btn-secondary\" onclick=\"exportDiagram('{diagram.Id}', 'html')\">Export HTML</button>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        
        // Diagram Container
        sb.AppendLine($"<div class=\"diagram-viewport\" id=\"diagram-{diagram.Id}\">");
        sb.AppendLine("<div class=\"mermaid\">");
        sb.AppendLine($"{diagram.MermaidContent}");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        
        // Component Info Panel
        sb.AppendLine($"<div class=\"component-info-panel\" id=\"info-{diagram.Id}\" style=\"display: none;\">");
        sb.AppendLine("<h5>Component Details</h5>");
        sb.AppendLine($"<div id=\"component-details-{diagram.Id}\"></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Script
        sb.AppendLine("<script>");
        sb.AppendLine("// Initialize diagram when DOM is ready");
        sb.AppendLine($"document.addEventListener('DOMContentLoaded', function() {{");
        sb.AppendLine($"    initializeDiagram('{diagram.Id}');");
        sb.AppendLine("}});");

        sb.AppendLine("// Diagram data for JavaScript");
        sb.AppendLine("window.diagramData = window.diagramData || {};");
        sb.AppendLine($"window.diagramData['{diagram.Id}'] = {{");
        sb.AppendLine($"    components: {JsonSerializer.Serialize(diagram.Components)},");
        sb.AppendLine($"    relationships: {JsonSerializer.Serialize(diagram.Relationships)},");
        sb.AppendLine($"    options: {JsonSerializer.Serialize(diagram.Options)}");
        sb.AppendLine("}};");
        sb.AppendLine("</script>");

        return sb.ToString();
    }

    /// <summary>
    ///     Generate component type options for filter dropdown
    /// </summary>
    private string GenerateComponentTypeOptions(List<DiagramComponent> components)
    {
        var types = components.Select(c => c.Type).Distinct().OrderBy(t => t);
        return string.Join("", types.Select(type => $"<option value=\"{type}\">{type}</option>"));
    }

    /// <summary>
    ///     Generate layer options for filter dropdown
    /// </summary>
    private string GenerateLayerOptions(List<DiagramComponent> components)
    {
        var layers = components.Select(c => c.Layer).Distinct().OrderBy(l => l);
        return string.Join("", layers.Select(layer => $"<option value=\"{layer}\">{layer}</option>"));
    }
}