using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Interface for hierarchical decomposition service
/// </summary>
public interface IHierarchicalDecompositionService
{
    Task<ModuleTree> DecomposeHierarchicallyAsync(string repositoryPath, CancellationToken cancellationToken = default);
}