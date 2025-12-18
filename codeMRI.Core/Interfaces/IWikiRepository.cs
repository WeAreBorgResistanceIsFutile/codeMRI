using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IWikiRepository
{
    Task SaveStructureAsync(string repoPath, WikiStructure structure);
    Task<WikiStructure?> GetStructureAsync(string repoPath);
    Task DeleteStructureAsync(string repoPath);

    Task SavePageAsync(string repoPath, WikiPage page);
    Task<WikiPage?> GetPageAsync(string repoPath, string pageId);
    Task<WikiPage?> GetPageByTitleAsync(string repoPath, string pageTitle);
    Task DeletePageAsync(string repoPath, string pageId);

    Task SaveIngestionManifestAsync(string repoPath, Dictionary<string, string> manifest);
    Task<Dictionary<string, string>> GetIngestionManifestAsync(string repoPath);
    Task DeleteIngestionManifestAsync(string repoPath);

    /// <summary>
    /// Saves the processing state for a repository ingestion
    /// </summary>
    Task SaveIngestionProcessingStateAsync(string repoPath, IngestionProcessingState state);

    /// <summary>
    /// Gets the processing state for a repository ingestion
    /// </summary>
    Task<IngestionProcessingState> GetIngestionProcessingStateAsync(string repoPath);

    /// <summary>
    /// Deletes the processing state for a repository ingestion
    /// </summary>
    Task DeleteIngestionProcessingStateAsync(string repoPath);

    /// <summary>
    /// Gets a list of all repositories that have been processed/stored.
    /// </summary>
    Task<List<string>> GetAllRepositoriesAsync();

    /// <summary>
    /// Gets all pages for a repository
    /// </summary>
    Task<List<WikiPage>> GetAllPagesAsync(string repoPath);

    /// <summary>
    /// Gets a list of repository summaries including ingestion status
    /// </summary>
    Task<List<RepositorySummary>> GetAllRepositorySummariesAsync();

    /// <summary>
    /// Updates the remote URL for a repository
    /// </summary>
    Task SetRepositoryRemoteUrlAsync(string repoPath, string remoteUrl);
}
