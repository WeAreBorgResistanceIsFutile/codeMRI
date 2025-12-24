using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Interface for hierarchical decomposition service
/// </summary>
public interface IHierarchicalDecompositionService
{
    Task<ModuleTree> DecomposeHierarchicallyAsync(string repositoryPath, EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
}