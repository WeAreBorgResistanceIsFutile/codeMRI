using System.Collections.Concurrent;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

public interface IEnhancedDependencyGraphService
{
    Task<EnhancedDependencyGraph> BuildGraphAsync(List<CodeComponent> components, CancellationToken cancellationToken = default);
    Task<GraphAnalysisResult> AnalyzeGraphAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<List<string>> IdentifyEntryPointsAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<Dictionary<string, double>> CalculateImportanceScoresAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<Dictionary<string, double>> EstimateTokensAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task<ModuleTree> DecomposeHierarchicallyAsync(EnhancedDependencyGraph graph, int maxTokensPerModule = 32768, CancellationToken cancellationToken = default);
    Task<List<CodeComponent>> GetComponentsAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<Dictionary<string, HashSet<string>>> PartitionByDirectoryStructureAsync(EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
}
