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
}