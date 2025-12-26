using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface ICodeWikiOrchestrator
{
    /// <summary>
    ///     Generates a complete wiki structure and content using the advanced CodeWiki workflow:
    ///     Decomposition -> Rubric -> Generation -> Judge -> Refinement
    /// </summary>
    Task<WikiStructure> GenerateAdvancedWikiAsync(
        string repositoryPath,
        RepositoryInfo repositoryInfo,
        IProgress<ProgressInfo>? progress = null,
        bool force = false,
        CancellationToken cancellationToken = default);
}