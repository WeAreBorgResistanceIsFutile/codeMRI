using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for dynamic delegation (subdivision) of modules during documentation generation.
///     Implements the "Dynamic Delegation" pattern from the CodeWiki paper.
/// </summary>
public interface IDelegationService
{
    /// <summary>
    ///     Evaluates whether a module requires delegation (further subdivision)
    ///     based on token count, complexity metrics, and semantic diversity.
    /// </summary>
    /// <param name="module">The module to evaluate</param>
    /// <param name="graph">The dependency graph for additional context</param>
    /// <param name="currentDepth">Current depth in the delegation hierarchy</param>
    /// <returns>A decision indicating whether to delegate and why</returns>
    DelegationDecision EvaluateDelegation(ModuleNode module, EnhancedDependencyGraph graph, int currentDepth);

    /// <summary>
    ///     Performs the actual delegation by subdividing the module into smaller sub-modules.
    ///     Updates the module tree in place and returns the new sub-modules.
    /// </summary>
    /// <param name="module">The module to subdivide</param>
    /// <param name="graph">The dependency graph for clustering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of newly created sub-modules</returns>
    Task<List<ModuleNode>> DelegateModuleAsync(ModuleNode module, EnhancedDependencyGraph graph,
        CancellationToken cancellationToken);
}