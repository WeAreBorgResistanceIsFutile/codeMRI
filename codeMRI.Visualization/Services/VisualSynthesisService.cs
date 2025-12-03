using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Visualization.Services;

public class VisualSynthesisService : IVisualSynthesisService
{
    private readonly IDiagramGenerator _generator;

    public VisualSynthesisService(IDiagramGenerator generator)
    {
        _generator = generator;
    }

    public async Task<VisualArtifacts> GenerateArtifactsAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph)
    {
        var artifacts = new VisualArtifacts();

        // 1. Architecture Diagram
        artifacts.ArchitectureDiagram = await _generator.GenerateArchitectureDiagramAsync(moduleTree, graph);

        // 2. Component Diagrams (Top 5 by PageRank/Degree)
        // Assuming PageRank is populated, or fallback to degree
        var topNodes = graph.GetNodes()
            .OrderByDescending(n => n.PageRankScore > 0 ? n.PageRankScore : n.OutDegree + n.InDegree)
            .Take(5);

        foreach (var node in topNodes)
        {
            var diagram = await _generator.GenerateComponentDiagramAsync(graph, node.ComponentId);
            artifacts.ComponentDiagrams[node.ComponentId] = diagram;
        }

        // 3. Sequence Diagrams (Top 3 Entry Points)
        // Entry points: Zero In-Degree or explicitly marked as Controller/API
        var entryPoints = graph.GetNodes()
            .Where(n => n.InDegree == 0 || n.Metadata.Type.Contains("Controller") || n.Metadata.Type.Contains("API"))
            .Take(3);

        foreach (var node in entryPoints)
        {
            var diagram = await _generator.GenerateSequenceDiagramAsync(graph, node.ComponentId);
            artifacts.SequenceDiagrams[node.ComponentId] = diagram;
        }

        return artifacts;
    }
}