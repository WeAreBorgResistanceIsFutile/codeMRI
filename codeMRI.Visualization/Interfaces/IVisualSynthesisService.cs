using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Visualization.Models;

namespace codeMRI.Visualization.Interfaces
{
    public interface IVisualSynthesisService
    {
        Task<VisualArtifacts> GenerateArtifactsAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph);
    }
}