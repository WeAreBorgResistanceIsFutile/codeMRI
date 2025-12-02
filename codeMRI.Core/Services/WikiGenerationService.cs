using codeMRI.Shared.Models;
using System.Xml.Linq;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Interfaces;
using codeMRI.Visualization.Models;
using System.Text;

namespace codeMRI.Core.Services;

public class WikiGenerationService : IWikiGenerationService
{
    private readonly ILLMClient _llmClient;
    private readonly IEmbedder _embedder;
    private readonly IVectorDatabase _vectorDb;
    private readonly IDiagramGenerator _diagramGenerator;
    private readonly IEnhancedDependencyGraphService _graphService; // Needed to fetch graph for diagrams

    public WikiGenerationService(
        ILLMClient llmClient, 
        IEmbedder embedder, 
        IVectorDatabase vectorDb,
        IDiagramGenerator diagramGenerator,
        IEnhancedDependencyGraphService graphService)
    {
        _llmClient = llmClient;
        _embedder = embedder;
        _vectorDb = vectorDb;
        _diagramGenerator = diagramGenerator;
        _graphService = graphService;
    }

    public async Task<WikiStructure> GenerateStructureAsync(string fileTree, string readme, string language = "English")
    {
        var prompt = PromptTemplates.StructurePrompt(fileTree, readme, language);
        var response = await _llmClient.ChatAsync("", prompt, new List<ChatMessage>());
        
        var cleanXml = response.Replace("```xml", "").Replace("```", "").Trim();
        try 
        {
            int start = cleanXml.IndexOf("<wiki_structure>");
            int end = cleanXml.LastIndexOf("</wiki_structure>");
            if (start >= 0 && end > start)
            {
                cleanXml = cleanXml.Substring(start, end - start + 17); 
            }

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
                    PageRefs = s.Element("pages")?.Elements("page_ref").Select(p => p.Value).ToList() ?? new()
                }).ToList() ?? new()
            };
            return structure; 
        }
        catch (Exception)
        {
            return new WikiStructure { Title = "Error generating structure", Sections = new() };
        }
    }

    public async Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths, Dictionary<string, string> fileContents, string language = "English")
    {
        if (filePaths == null || filePaths.Count == 0)
        {
            var queryEmbedding = await _embedder.EmbedAsync(pageTitle);
            var docs = await _vectorDb.SearchAsync(queryEmbedding, topK: 5);
            filePaths = docs.Select(d => d.FilePath).Distinct().ToList();
            
            foreach (var doc in docs)
            {
                if (!fileContents.ContainsKey(doc.FilePath))
                {
                    fileContents[doc.FilePath] = doc.Content;
                }
            }
        }

        var contextBuilder = new StringBuilder();
        foreach (var path in filePaths)
        {
            if (fileContents.ContainsKey(path))
            {
                contextBuilder.AppendLine($"File: {path}");
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine(fileContents[path]);
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine();
            }
        }
        
        var prompt = PromptTemplates.PagePrompt(pageTitle, filePaths, language);
        var fullPrompt = prompt + "\n\nSOURCE FILES CONTENT:\n" + contextBuilder.ToString();

        var content = await _llmClient.ChatAsync("", fullPrompt, new List<ChatMessage>());

        // Generate enhanced interactive diagrams if we have a dependency graph
        try
        {
            var graph = await _graphService.BuildGraphAsync(new List<CodeComponent>(), CancellationToken.None);
            if (graph.NodeCount > 0)
            {
                // Try to find a component that matches the page title or file paths
                var entryPointId = FindEntryPointForPage(pageTitle, filePaths, graph);
                
                if (!string.IsNullOrEmpty(entryPointId))
                {
                    // Generate interactive sequence diagram
                    if (_diagramGenerator is codeMRI.Visualization.Services.DiagramGeneratorService enhancedGenerator)
                    {
                        var interactiveSequenceDiagram = await enhancedGenerator.GenerateInteractiveSequenceDiagramAsync(graph, entryPointId);
                        if (interactiveSequenceDiagram != null && !string.IsNullOrWhiteSpace(interactiveSequenceDiagram.MermaidContent))
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
                            content += sequenceDiagram + "\n";
                        }
                    }

                    // Generate interactive component diagram
                    if (_diagramGenerator is codeMRI.Visualization.Services.DiagramGeneratorService enhancedComponentGenerator)
                    {
                        var interactiveComponentDiagram = await enhancedComponentGenerator.GenerateInteractiveComponentDiagramAsync(graph, entryPointId);
                        if (interactiveComponentDiagram != null && !string.IsNullOrWhiteSpace(interactiveComponentDiagram.MermaidContent))
                        {
                            content += "\n\n## Interactive Component Diagram\n\n";
                            content += GenerateInteractiveDiagramHtml(interactiveComponentDiagram, "Component Diagram");
                        }
                    }
                    else
                    {
                        // Fallback to basic diagram generation
                        var componentDiagram = await _diagramGenerator.GenerateComponentDiagramAsync(graph, entryPointId);
                        if (!string.IsNullOrWhiteSpace(componentDiagram) && componentDiagram.Contains("classDiagram"))
                        {
                            content += "\n## Component Diagram\n\n";
                            content += componentDiagram + "\n";
                        }
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

        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = pageTitle,
            Content = content,
            RelevantFiles = filePaths
        };
    }

    public async Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages, string language = "English")
    {
        // 1. Generate Architectural Overview
        var sb = new StringBuilder();
        sb.AppendLine($"Synthesize an architectural overview for the module: {module.Name}");
        sb.AppendLine($"This module is at level {module.Level} in the hierarchy.");
        
        if (!string.IsNullOrEmpty(module.Description))
        {
            sb.AppendLine($"Module Description: {module.Description}");
        }
        
        sb.AppendLine("\nSub-modules/Components:");
        foreach (var page in childPages)
        {
            sb.AppendLine($"- **{page.Title}**: {ExtractSummary(page.Content)}");
        }
        
        sb.AppendLine("\nInstructions:");
        sb.AppendLine("1. Create a high-level overview of this module's responsibilities.");
        sb.AppendLine("2. Explain how the sub-modules interact and contribute to the overall goal.");
        sb.AppendLine("3. Identify key architectural patterns used in this module.");
        sb.AppendLine($"4. Write the response in {language}.");
        
        var prompt = sb.ToString();
        var content = await _llmClient.ChatAsync("", prompt, new List<ChatMessage>());
        
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
                if (!string.IsNullOrWhiteSpace(architectureDiagram) && architectureDiagram.Contains("graph TD"))
                {
                    content += "\n\n## Architecture Diagram\n\n";
                    content += architectureDiagram + "\n";
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the page generation
            Console.WriteLine($"Failed to generate architecture diagram for module {module.Name}: {ex.Message}");
        }
        
        return new WikiPage
        {
            Id = Guid.NewGuid().ToString(),
            Title = module.Name,
            Content = content,
            RelevantFiles = new List<string>() 
        };
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

        if (titleCandidates.Any())
        {
            return titleCandidates.First().ComponentId;
        }

        // Try to match based on file paths
        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var fileCandidates = graph.GetNodes()
                .Where(n => n.ComponentId.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                            n.ComponentId.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
                            fileName.Contains(n.ComponentId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (fileCandidates.Any())
            {
                return fileCandidates.First().ComponentId;
            }
        }

        // If no specific match, return the first node with outgoing edges (likely an entry point)
        var entryPoint = graph.GetNodes()
            .Where(n => n.OutDegree > 0)
            .OrderByDescending(n => n.OutDegree)
            .FirstOrDefault();

        return entryPoint?.ComponentId;
    }

    /// <summary>
    /// Generate HTML wrapper for interactive diagrams with zoom, filter, and export controls
    /// </summary>
    private string GenerateInteractiveDiagramHtml(InteractiveDiagram diagram, string title)
    {
        var html = $@"
<div class=""interactive-diagram-container"" data-diagram-id=""{diagram.Id}"">
    <h3>{title}</h3>
    
    <!-- Control Panel -->
    <div class=""diagram-controls"">
        <!-- Zoom Controls -->
        <div class=""zoom-controls"">
            <button class=""btn btn-sm btn-outline-secondary"" onclick=""zoomIn('{diagram.Id}')"">+</button>
            <button class=""btn btn-sm btn-outline-secondary"" onclick=""zoomOut('{diagram.Id}')"">-</button>
            <button class=""btn btn-sm btn-outline-secondary"" onclick=""resetZoom('{diagram.Id}')"">Reset</button>
            <button class=""btn btn-sm btn-outline-secondary"" onclick=""fitToView('{diagram.Id}')"">Fit</button>
        </div>
        
        <!-- Filter Controls -->
        <div class=""filter-controls"">
            <select class=""form-select form-select-sm"" id=""filter-type-{diagram.Id}"" onchange=""applyFilters('{diagram.Id}')"">
                <option value="""">All Types</option>
                {GenerateComponentTypeOptions(diagram.Components)}
            </select>
            
            <select class=""form-select form-select-sm"" id=""filter-layer-{diagram.Id}"" onchange=""applyFilters('{diagram.Id}')"">
                <option value="""">All Layers</option>
                {GenerateLayerOptions(diagram.Components)}
            </select>
            
            <input type=""range"" class=""form-range"" id=""filter-complexity-{diagram.Id}"" 
                   min=""0"" max=""100"" value=""100"" onchange=""applyFilters('{diagram.Id}')""
                   title=""Filter by complexity"">
        </div>
        
        <!-- Export Controls -->
        <div class=""export-controls"">
            <button class=""btn btn-sm btn-primary"" onclick=""exportDiagram('{diagram.Id}', 'png')"">Export PNG</button>
            <button class=""btn btn-sm btn-primary"" onclick=""exportDiagram('{diagram.Id}', 'svg')"">Export SVG</button>
            <button class=""btn btn-sm btn-secondary"" onclick=""exportDiagram('{diagram.Id}', 'html')"">Export HTML</button>
        </div>
    </div>
    
    <!-- Diagram Container -->
    <div class=""diagram-viewport"" id=""diagram-{diagram.Id}"">
        <div class=""mermaid"">
            {diagram.MermaidContent}
        </div>
    </div>
    
    <!-- Component Info Panel -->
    <div class=""component-info-panel"" id=""info-{diagram.Id}"" style=""display: none;"">
        <h5>Component Details</h5>
        <div id=""component-details-{diagram.Id}""></div>
    </div>
</div>

<script>
// Initialize diagram when DOM is ready
document.addEventListener('DOMContentLoaded', function() {{
    initializeDiagram('{diagram.Id}');
}});

// Diagram data for JavaScript
window.diagramData = window.diagramData || {{}};
window.diagramData['{diagram.Id}'] = {{
    components: {System.Text.Json.JsonSerializer.Serialize(diagram.Components)},
    relationships: {System.Text.Json.JsonSerializer.Serialize(diagram.Relationships)},
    options: {System.Text.Json.JsonSerializer.Serialize(diagram.Options)}
}};
</script>";

        return html;
    }

    /// <summary>
    /// Generate component type options for filter dropdown
    /// </summary>
    private string GenerateComponentTypeOptions(List<DiagramComponent> components)
    {
        var types = components.Select(c => c.Type).Distinct().OrderBy(t => t);
        return string.Join("", types.Select(type => $"<option value=\"{type}\">{type}</option>"));
    }

    /// <summary>
    /// Generate layer options for filter dropdown
    /// </summary>
    private string GenerateLayerOptions(List<DiagramComponent> components)
    {
        var layers = components.Select(c => c.Layer).Distinct().OrderBy(l => l);
        return string.Join("", layers.Select(layer => $"<option value=\"{layer}\">{layer}</option>"));
    }
}
