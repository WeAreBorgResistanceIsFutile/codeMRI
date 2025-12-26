using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.Decorators;

/// <summary>
///     Decorator for IWikiGenerationService that adds resumability by skipping already generated pages
/// </summary>
public class ResumableWikiGenerationDecorator : IWikiGenerationService
{
    private readonly IWikiGenerationService _inner;
    private readonly IWikiRepository _wikiRepo;
    private readonly ILLMInvocationContext _invocationContext;
    private readonly ILogger<ResumableWikiGenerationDecorator> _logger;

    public ResumableWikiGenerationDecorator(
        IWikiGenerationService inner,
        IWikiRepository wikiRepo,
        ILLMInvocationContext invocationContext,
        ILogger<ResumableWikiGenerationDecorator> logger)
    {
        _inner = inner;
        _wikiRepo = wikiRepo;
        _invocationContext = invocationContext;
        _logger = logger;
    }

    public async Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths, Dictionary<string, string> fileContents, string language = "English", string? repoPath = null, string? remoteUrl = null, string? branch = null)
    {
        if (!string.IsNullOrEmpty(repoPath) && !_invocationContext.Force)
        {
            var existingPage = await _wikiRepo.GetPageByTitleAsync(repoPath, pageTitle);
            if (existingPage != null && !string.IsNullOrEmpty(existingPage.Content))
            {
                _logger.LogInformation("Skipping page generation: Page '{Title}' already exists in repository.", pageTitle);
                return existingPage;
            }
        }

        return await _inner.GeneratePageAsync(pageTitle, filePaths, fileContents, language, repoPath, remoteUrl, branch);
    }

    public async Task<WikiPage> GenerateEnhancedPageAsync(ModuleNode module, List<WikiPage>? relatedPages, ModulePageContext context, Dictionary<string, string> fileContents, string language = "English", string? repoPath = null, AudienceType audience = AudienceType.Developer, string? remoteUrl = null, string? branch = null, List<string>? explicitFilePaths = null)
    {
        // Set context for potential error snapshots
        _invocationContext.ComponentId = module.Id;
        _invocationContext.RepoPath = repoPath;

        if (!string.IsNullOrEmpty(repoPath) && !_invocationContext.Force)
        {
            // Try to find by ID first, then title
            var existingPage = await _wikiRepo.GetPageAsync(repoPath, module.Id) ?? await _wikiRepo.GetPageByTitleAsync(repoPath, module.Name);
            
            if (existingPage != null && !string.IsNullOrEmpty(existingPage.Content))
            {
                _logger.LogInformation("Skipping enhanced page generation: Module '{Name}' ({Id}) already exists.", module.Name, module.Id);
                return existingPage;
            }
        }

        return await _inner.GenerateEnhancedPageAsync(module, relatedPages, context, fileContents, language, repoPath, audience, remoteUrl, branch, explicitFilePaths);
    }

    public async Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages, string language = "English", AudienceType audience = AudienceType.Developer)
    {
        // We don't have repoPath here directly, so we might not be able to skip easily unless we pass it in.
        // For parent pages, they are usually cheap synthesis.
        return await _inner.GenerateParentPageAsync(module, childPages, language, audience);
    }
}
