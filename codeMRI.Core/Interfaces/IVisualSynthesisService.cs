using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IVisualSynthesisService
{
    Task<VisualArtifacts> GenerateArtifactsAsync(ModuleTree moduleTree, EnhancedDependencyGraph graph);
}