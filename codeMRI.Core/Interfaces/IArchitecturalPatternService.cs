using codeMRI.Core.Models;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces
{
    public interface IArchitecturalPatternService
    {
        ArchitecturalPattern RecognizePattern(ModuleNode module, EnhancedDependencyGraph graph);
        ArchitecturalLayerType DetermineLayer(GraphNode node);
    }
}
