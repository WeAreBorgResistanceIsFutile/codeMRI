namespace codeMRI.Visualization.Interfaces
{
    public interface IDiagramGenerator
    {
        Task<string> GenerateArchitectureDiagramAsync(Core.Models.ModuleTree moduleTree, Core.Interfaces.EnhancedDependencyGraph graph);
        Task<string> GenerateComponentDiagramAsync(Core.Interfaces.EnhancedDependencyGraph graph, string? focusComponentId = null);
        Task<string> GenerateSequenceDiagramAsync(Core.Interfaces.EnhancedDependencyGraph graph, string entryPointId);
        Task<string> GenerateDataFlowDiagramAsync(Core.Interfaces.EnhancedDependencyGraph graph, string focusComponentId);
    }
}