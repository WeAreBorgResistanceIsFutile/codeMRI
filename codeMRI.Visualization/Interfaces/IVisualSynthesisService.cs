using codeMRI.Shared.Models;
using codeMRI.Visualization.Models;

namespace codeMRI.Visualization.Interfaces
{
    public interface IVisualSynthesisService
    {
        Task<VisualArtifacts> GenerateArtifactsAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph);
    }
}