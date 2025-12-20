using codeMRI.Core.Models;
using codeMRI.Core.Services;

namespace codeMRI.Core.Interfaces;

public interface IWikiGenerationService
{
    /// <summary>
    ///     Generates a wiki page for the given title using file paths and contents.
    ///     Uses the legacy PagePrompt for backward compatibility.
    /// </summary>
    Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths, Dictionary<string, string> fileContents,
        string language = "English", string? repoPath = null, string? remoteUrl = null, string? branch = null);

    /// <summary>
    ///     Generates a wiki page using the enhanced prompt with full module context.
    ///     This is the preferred method for advanced wiki generation.
    /// </summary>
    Task<WikiPage> GenerateEnhancedPageAsync(
        ModuleNode module,
        List<WikiPage>? relatedPages,
        ModulePageContext context,
        Dictionary<string, string> fileContents,
        string language = "English",
        string? repoPath = null,
        AudienceType audience = AudienceType.Developer,
        string? remoteUrl = null,
        string? branch = null,
        List<string>? explicitFilePaths = null);

    /// <summary>
    ///     Generates a parent/overview page by synthesizing child pages.
    /// </summary>
    Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages, string language = "English",
        AudienceType audience = AudienceType.Developer);
}