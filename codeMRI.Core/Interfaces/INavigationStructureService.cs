using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service responsible for transforming code structure (ModuleTree) into
///     documentation-optimized navigation structure (WikiStructure) using LLM analysis
/// </summary>
public interface INavigationStructureService
{
    /// <summary>
    ///     Generates a documentation-focused navigation structure from the hierarchical module tree
    /// </summary>
    /// <param name="moduleTree">The hierarchical decomposition of the codebase</param>
    /// <param name="repositoryInfo">Repository metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>WikiStructure optimized for documentation, not code organization</returns>
    Task<WikiStructure> GenerateDocumentationStructureAsync(
        ModuleTree moduleTree,
        RepositoryInfo repositoryInfo,
        CancellationToken cancellationToken = default);
}
