using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class WikiGenerationService : IWikiGenerationService
{
    private readonly IDiagramGenerator _diagramGenerator;
    private readonly IEmbedder _embedder;
    private readonly IEnhancedDependencyGraphService _graphService; // Needed to fetch graph for diagrams
    private readonly ILLMClient _llmClient;
    private readonly IVectorDatabase _vectorDb;
    private readonly IDocumentationSynthesisService _synthesisService;
    private readonly IReferenceManagementService _referenceManagementService;
    private readonly string _documentationModel;

    public WikiGenerationService(
        ILLMClient llmClient,
        IEmbedder embedder,
        IVectorDatabase vectorDb,
        IDiagramGenerator diagramGenerator,
        IEnhancedDependencyGraphService graphService,
        IDocumentationSynthesisService synthesisService,
        IReferenceManagementService referenceManagementService,
        string documentationModel = "llama3")
    {
        _llmClient = llmClient;
        _embedder = embedder;
        _vectorDb = vectorDb;
        _diagramGenerator = diagramGenerator;
        _graphService = graphService;
        _synthesisService = synthesisService;
        _synthesisService = synthesisService;
        _referenceManagementService = referenceManagementService;
        _documentationModel = documentationModel;
    }

    public async Task<WikiStructure> GenerateStructureAsync(string fileTree, string readme, string language = "English")
    {
        var prompt = PromptTemplates.StructurePrompt(fileTree, readme, language);
        var response = await _llmClient.ChatAsync("", prompt, new List<ChatMessage>(), _documentationModel);

        var cleanXml = response.Replace("```xml", "").Replace("```", "").Trim();
        try
        {
            var start = cleanXml.IndexOf("<wiki_structure>");
            var end = cleanXml.LastIndexOf("</wiki_structure>");
            if (start >= 0 && end > start) cleanXml = cleanXml.Substring(start, end - start + 17);

            var doc = XDocument.Parse(cleanXml);
            var root = doc.Element("wiki_structure");

            var structure = new WikiStructure
            {
                Title = root?.Element("title")?.Value ?? "Wiki",
                Description = root?.Element("description")?.Value ?? "",
                Sections = root?.Element("sections")?.Elements("section").Select(s => new WikiSection
                {
                    Id = s.Attribute("id")?.Value ?? Guid.NewGuid().ToString(),
                    Title = s.Element("title")?.Value ?? "Section",
                    PageRefs = s.Element("pages")?.Elements("page_ref").Select(p => p.Value).ToList() ??
                               new List<string>()
                }).ToList() ?? new List<WikiSection>()
            };
            return structure;
        }
        catch (Exception)
        {
            return new WikiStructure { Title = "Error generating structure", Sections = new List<WikiSection>() };
        }
    }

    public async Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths,
        Dictionary<string, string> fileContents, string language = "English")
    {
        if (filePaths == null || filePaths.Count == 0)
        {
            var queryEmbedding = await _embedder.EmbedAsync(pageTitle);
            var docs = await _vectorDb.SearchAsync(queryEmbedding, 5);
            filePaths = docs.Select(d => d.FilePath).Distinct().ToList();

            foreach (var doc in docs)
                if (!fileContents.ContainsKey(doc.FilePath))
                    fileContents[doc.FilePath] = doc.Content;
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
            Console.WriteLine($"Failed to generate diagrams for page {pageTitle}: {ex.Message}");
        }

        // Enrich content with intelligent cross-links
        // Use pageTitle as sourceComponentId context if possible, or a safe fallback
        var pageId = Guid.NewGuid().ToString();
        content = _referenceManagementService.EnrichContentWithLinks(content, pageTitle); // Using title as ID proxy for now

        return new WikiPage
        {
            Id = pageId,
            Title = pageTitle,
            Content = content,
            RelevantFiles = filePaths
        };
    }

    public async Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages,
        string language = "English")
    {
        // 1. Delegate synthesis to the specialized service
        var page = await _synthesisService.SynthesizeParentPageAsync(module, childPages, language);
        var content = page.Content;

        // 2. Generate Architecture Diagram
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
            Console.WriteLine($"Failed to generate architecture diagram for module {module.Name}: {ex.Message}");
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
        sb.AppendLine($"    <h3>{title}</h3>");
        
        // Control Panel
        sb.AppendLine("    <div class=\"diagram-controls\">");
        // Zoom Controls
        sb.AppendLine("        <div class=\"zoom-controls\">");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-outline-secondary\" onclick=\"zoomIn('{diagram.Id}')\">+</button>");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-outline-secondary\" onclick=\"zoomOut('{diagram.Id}')\">-</button>");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-outline-secondary\" onclick=\"resetZoom('{diagram.Id}')\">Reset</button>");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-outline-secondary\" onclick=\"fitToView('{diagram.Id}')\">Fit</button>");
        sb.AppendLine("        </div>");
        
        // Filter Controls
        sb.AppendLine("        <div class=\"filter-controls\">");
        sb.AppendLine($"            <select class=\"form-select form-select-sm\" id=\"filter-type-{diagram.Id}\" onchange=\"applyFilters('{diagram.Id}')\">");
        sb.AppendLine("                <option value=\"\">All Types</option>");
        sb.AppendLine($"                {GenerateComponentTypeOptions(diagram.Components)}");
        sb.AppendLine("            </select>");
        
        sb.AppendLine($"            <select class=\"form-select form-select-sm\" id=\"filter-layer-{diagram.Id}\" onchange=\"applyFilters('{diagram.Id}')\">");
        sb.AppendLine("                <option value=\"\">All Layers</option>");
        sb.AppendLine($"                {GenerateLayerOptions(diagram.Components)}");
        sb.AppendLine("            </select>");
        
        sb.AppendLine($"            <input type=\"range\" class=\"form-range\" id=\"filter-complexity-{diagram.Id}\" ");
        sb.AppendLine($"                   min=\"0\" max=\"100\" value=\"100\" onchange=\"applyFilters('{diagram.Id}')\"");
        sb.AppendLine("                   title=\"Filter by complexity\">");
        sb.AppendLine("        </div>");
        
        // Export Controls
        sb.AppendLine("        <div class=\"export-controls\">");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-primary\" onclick=\"exportDiagram('{diagram.Id}', 'png')\">Export PNG</button>");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-primary\" onclick=\"exportDiagram('{diagram.Id}', 'svg')\">Export SVG</button>");
        sb.AppendLine($"            <button class=\"btn btn-sm btn-secondary\" onclick=\"exportDiagram('{diagram.Id}', 'html')\">Export HTML</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        
        // Diagram Container
        sb.AppendLine($"    <div class=\"diagram-viewport\" id=\"diagram-{diagram.Id}\">");
        sb.AppendLine("        <div class=\"mermaid\">");
        sb.AppendLine($"            {diagram.MermaidContent}");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        
        // Component Info Panel
        sb.AppendLine($"    <div class=\"component-info-panel\" id=\"info-{diagram.Id}\" style=\"display: none;\">");
        sb.AppendLine("        <h5>Component Details</h5>");
        sb.AppendLine($"        <div id=\"component-details-{diagram.Id}\"></div>");
        sb.AppendLine("    </div>");
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