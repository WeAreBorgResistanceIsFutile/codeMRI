using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IDocumentIndexer
{
    Task IndexDocumentationAsync(string repoPath, WikiStructure structure, CancellationToken cancellationToken = default);
    Task IndexPageAsync(string repoPath, WikiPage page, CancellationToken cancellationToken = default);
    Task IndexCodebaseAsync(string repoPath, EnhancedDependencyGraph graph, CancellationToken cancellationToken = default);
    Task DeleteIndexAsync(string repoPath, CancellationToken cancellationToken = default);
}
