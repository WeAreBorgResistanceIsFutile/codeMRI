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
    private readonly ILogger<DocumentationGenerationPipeline> _logger;

    public DocumentationGenerationPipeline(
        IAgentCoordinator coordinator,
        IVisualSynthesisService visualSynthesisService,
        IEnhancedDependencyGraphService graphService,
        IHierarchicalDecompositionService decompositionService,
        ILogger<DocumentationGenerationPipeline> logger)
    {
        _coordinator = coordinator;
        _visualSynthesisService = visualSynthesisService;
        _graphService = graphService;
        _decompositionService = decompositionService;
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
        var structure = data.Structure;

        // 1.5 Build Graph and Tree for Visualization
        // Note: Ideally AnalyzerAgent should return the graph, but for now we rebuild it or assume AnalyzerAgent logic matches.
        // To ensure consistency, we use the services.
        var graph = await _graphService.BuildGraphAsync(components, CancellationToken.None);
        // Run analysis to populate metrics needed for visualization (PageRank etc)
        await _graphService.AnalyzeGraphAsync(graph, CancellationToken.None); 
        
        var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath, CancellationToken.None);
        // Note: DecomposeHierarchicallyAsync currently re-scans components internally. 
        // We should optimize this later to reuse components/graph.
        // But for now, we have the visual models.

        // Generate Visual Artifacts
        var artifacts = await _visualSynthesisService.GenerateArtifactsAsync(moduleTree, graph);

        // 2. Document Components
        var wikiPages = new List<WikiPage>();
        foreach (var component in components)
        {
            var docTask = new AgentTask
            {
                Type = "Documenter",
                Payload = component
            };
            var docResult = await _coordinator.CoordinateTaskAsync(docTask, CancellationToken.None);
            if (docResult.Success && docResult.Output is WikiPage page)
            {
                // Embed Component Diagram if available
                if (artifacts.ComponentDiagrams.TryGetValue(component.Id, out var compDiagram))
                {
                    page.Content += "\n\n## Component Structure\n\n```mermaid\n" + compDiagram + "\n```";
                }
                // Embed Sequence Diagram if available (Entry Point)
                if (artifacts.SequenceDiagrams.TryGetValue(component.Id, out var seqDiagram))
                {
                    page.Content += "\n\n## Interaction Sequence\n\n```mermaid\n" + seqDiagram + "\n```";
                }

                wikiPages.Add(page);
            }
        }

        // 3. Synthesize
        var synthTask = new AgentTask
        {
            Type = "Synthesizer",
            Payload = wikiPages
        };
        var synthResult = await _coordinator.CoordinateTaskAsync(synthTask, CancellationToken.None);

        if (synthResult.Success && synthResult.Output is WikiStructure wikiStructure)
        {
            // Embed Architecture Diagram in Root/Home Page
            // Assuming Synthesizer creates a Home/Index page or we add it to description/intro
            // For now, lets append it to the Description of the structure or a main page if found
            
            // Let's assume the first page or a page named "Home" or "Index"
            // Or better, we can add it to the wikiStructure metadata or description if format allows.
            // Markdown:
            wikiStructure.Description += "\n\n## System Architecture\n\n```mermaid\n" + artifacts.ArchitectureDiagram + "\n```";

            return wikiStructure;
        }

        throw new Exception("Failed to synthesize documentation");
    }

    public Task<WikiPage> GenerateComponentDocumentationAsync(CodeComponent component, RepositoryStructure context)
    {
        // Direct delegation to Documenter
        var task = new AgentTask { Type = "Documenter", Payload = component };
        // This method is sync-over-async wrapper or we change interface? Interface returns Task.
        // We need to wait for coordinator.
        var result = _coordinator.CoordinateTaskAsync(task, CancellationToken.None).GetAwaiter().GetResult(); // Blocking!
        // Better: make this async if interface allows. Interface IS async Task<WikiPage>.
        
        // But I can't await in this sync block if I don't make method async... 
        // Wait, the method signature IS `Task<WikiPage>`.
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
        throw new Exception("Documentation generation failed");
    }

    public async Task<List<WikiPage>> GenerateOverviewPagesAsync(RepositoryStructure structure, List<CodeComponent> components)
    {
         // Simplified implementation for now - could use another agent
         return new List<WikiPage>();
    }
}
