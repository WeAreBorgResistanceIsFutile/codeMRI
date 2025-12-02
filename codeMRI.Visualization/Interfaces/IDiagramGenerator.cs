using codeMRI.Shared.Models;

namespace codeMRI.Visualization.Interfaces;

public interface IDiagramGenerator
{
    Task<string> GenerateArchitectureDiagramAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph);
    Task<string> GenerateComponentDiagramAsync(EnhancedDependencyGraph graph, string? focusComponentId = null);
    Task<string> GenerateSequenceDiagramAsync(EnhancedDependencyGraph graph, string entryPointId);
    Task<string> GenerateDataFlowDiagramAsync(EnhancedDependencyGraph graph, string focusComponentId);
}