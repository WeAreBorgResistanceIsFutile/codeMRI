using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IDiagramGenerator
{
    Task<string> GenerateArchitectureDiagramAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph);
    Task<string> GenerateComponentDiagramAsync(EnhancedDependencyGraph graph, string? focusComponentId = null);
    Task<string> GenerateSequenceDiagramAsync(EnhancedDependencyGraph graph, string entryPointId);
    Task<string> GenerateDataFlowDiagramAsync(EnhancedDependencyGraph graph, string focusComponentId);

    Task<InteractiveDiagram> GenerateInteractiveComponentDiagramAsync(EnhancedDependencyGraph graph,
        string? focusComponentId = null,
        DiagramOptions? options = null);

    Task<InteractiveDiagram> GenerateInteractiveSequenceDiagramAsync(EnhancedDependencyGraph graph,
        string entryPointId,
        DiagramOptions? options = null);
}