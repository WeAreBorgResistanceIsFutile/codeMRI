using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Shared.Models;
using codeMRI.Visualization.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

public class DocumentationGenerationPipeline : IDocumentationGenerationPipeline
{
    private readonly IAgentCoordinator _coordinator;
    private readonly IVisualSynthesisService _visualSynthesisService;
    private readonly IEnhancedDependencyGraphService _graphService;
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly IWikiGenerationService _wikiGenService;
    private readonly ILogger<DocumentationGenerationPipeline> _logger;

    public DocumentationGenerationPipeline(
        IAgentCoordinator coordinator,
        IVisualSynthesisService visualSynthesisService,
        IEnhancedDependencyGraphService graphService,
        IHierarchicalDecompositionService decompositionService,
        IWikiGenerationService wikiGenService,
        ILogger<DocumentationGenerationPipeline> logger)
    {
        _coordinator = coordinator;
        _visualSynthesisService = visualSynthesisService;
        _graphService = graphService;
        _decompositionService = decompositionService;
        _wikiGenService = wikiGenService;
        _logger = logger;
    }

    public async Task<WikiStructure> GenerateDocumentationAsync(string repositoryPath, DocumentationOptions options)
    {
        _logger.LogInformation("Starting agent-based documentation generation for: {RepoPath}", repositoryPath);

        // 1. Analyze
        var analysisTask = new AgentTask
        {
            Type = "Analyzer",
            Payload = repositoryPath
        };

        var analysisResult = await _coordinator.CoordinateTaskAsync(analysisTask, CancellationToken.None);
        if (!analysisResult.Success)
        {
            throw new Exception($"Analysis failed: {string.Join(", ", analysisResult.Errors)}");
        }

        var data = (AnalysisResult)analysisResult.Output!;
        var components = data.Components;
        // var structure = data.Structure; // Unused for now

        // 1.5 Build Graph and Tree for Visualization
        var graph = await _graphService.BuildGraphAsync(components, CancellationToken.None);
        await _graphService.AnalyzeGraphAsync(graph, CancellationToken.None); 
        
        var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath, CancellationToken.None);

        // Generate Visual Artifacts
        var artifacts = await _visualSynthesisService.GenerateArtifactsAsync(moduleTree, graph);

        // 2. Hierarchical Documentation Generation (Post-Order Traversal)
        var allWikiPages = new List<WikiPage>();
        
        // Create a map of component ID to CodeComponent for easy access
        var componentMap = components.ToDictionary(c => c.Id, c => c);
        
        // Process the tree recursively
        await ProcessModuleNodeAsync(moduleTree.Root, componentMap, allWikiPages, artifacts);

        // 3. Synthesize (Optional final wrap-up or structure generation)
        // For now, we rely on the structure we built.
        // We can return a WikiStructure object reflecting the tree.
        
        var wikiStructure = new WikiStructure
        {
            Title = "Codebase Documentation",
            Description = "Automatically generated documentation.",
            Sections = new List<WikiSection>()
        };
        
        // Add architecture diagram to root page if exists
        var rootPage = allWikiPages.FirstOrDefault(p => p.Title == moduleTree.Root.Name);
        if (rootPage != null && artifacts.ArchitectureDiagram != null)
        {
             rootPage.Content += "\n\n## System Architecture\n\n```mermaid\n" + artifacts.ArchitectureDiagram + "\n```";
        }

        // Convert pages to structure (simplified flat list of sections for now, or we could build hierarchy)
        // Ideally we map the ModuleTree structure to WikiStructure.
        
        return wikiStructure;
    }

    private async Task<WikiPage?> ProcessModuleNodeAsync(
        ModuleNode node, 
        Dictionary<string, CodeComponent> componentMap,
        List<WikiPage> allPages,
        Visualization.Models.VisualArtifacts artifacts)
    {
        var childPages = new List<WikiPage>();

        // 1. Visit Children First (Post-Order)
        foreach (var child in node.Children)
        {
            var page = await ProcessModuleNodeAsync(child, componentMap, allPages, artifacts);
            if (page != null)
            {
                childPages.Add(page);
            }
        }

        WikiPage? currentNodePage = null;

        // 2. Generate Docs for Leaf Components in this Module
        // The ModuleNode contains 'Components' which are IDs of CodeComponents.
        // Note: ModuleNode might represent a folder or a logical grouping.
        // If it has direct components (that are not sub-modules), we document them.
        // BUT, checking ModuleNode definition: it has Children (ModuleNodes) and Components (HashSet<string>).
        // So a Module can have both sub-modules and direct components.
        
        // In "Post-Order", we should also process the direct components of this module before generating the module summary.
        foreach (var compId in node.Components)
        {
            if (componentMap.TryGetValue(compId, out var component))
            {
                var compPage = await GenerateComponentDocumentationAsyncInternal(component);
                
                // Embed diagrams
                if (artifacts.ComponentDiagrams.TryGetValue(component.Id, out var compDiagram))
                {
                    compPage.Content += "\n\n## Component Structure\n\n```mermaid\n" + compDiagram + "\n```";
                }
                if (artifacts.SequenceDiagrams.TryGetValue(component.Id, out var seqDiagram))
                {
                    compPage.Content += "\n\n## Interaction Sequence\n\n```mermaid\n" + seqDiagram + "\n```";
                }
                
                allPages.Add(compPage);
                childPages.Add(compPage); // Treat components as "children" for the module summary
            }
        }

        // 3. Generate Module Documentation (Parent) using Child Summaries
        // Only generate if it's not a purely leaf node with no children, 
        // OR if it's a leaf node (Module) that groups components.
        // Actually, every ModuleNode needs a page if it represents a directory/module.
        
        if (childPages.Count > 0 || node.Children.Count > 0)
        {
            currentNodePage = await _wikiGenService.GenerateParentPageAsync(node, childPages);
            allPages.Add(currentNodePage);
        }
        
        return currentNodePage;
    }

    public Task<WikiPage> GenerateComponentDocumentationAsync(CodeComponent component, RepositoryStructure context)
    {
         return GenerateComponentDocumentationAsyncInternal(component);
    }

    private async Task<WikiPage> GenerateComponentDocumentationAsyncInternal(CodeComponent component)
    {
        var task = new AgentTask { Type = "Documenter", Payload = component };
        var result = await _coordinator.CoordinateTaskAsync(task, CancellationToken.None);
        if (result.Success && result.Output is WikiPage page)
        {
            return page;
        }
        
        _logger.LogError("Failed to generate documentation for component {ComponentName}", component.Name);
        return new WikiPage 
        { 
            Title = component.Name, 
            Content = "Documentation generation failed." 
        };
    }

    public async Task<List<WikiPage>> GenerateOverviewPagesAsync(RepositoryStructure structure, List<CodeComponent> components)
    {
         return new List<WikiPage>();
    }
}
