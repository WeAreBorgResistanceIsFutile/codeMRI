using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Interface for hierarchical decomposition service
/// </summary>
public interface IHierarchicalDecompositionService
{
    Task<ModuleTree> DecomposeHierarchicallyAsync(string repositoryPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);
}