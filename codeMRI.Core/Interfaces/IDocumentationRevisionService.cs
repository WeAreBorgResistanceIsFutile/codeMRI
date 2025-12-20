using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for revising parent documentation based on insights from child module documentation.
///     Implements the revision loop from Algorithm 1 in the CodeWiki paper.
/// </summary>
public interface IDocumentationRevisionService
{
    /// <summary>
    ///     Revises a parent page based on detailed insights from child documentation.
    /// </summary>
    /// <param name="parentPage">The initial synthesized parent page</param>
    /// <param name="parentModule">The parent module being documented</param>
    /// <param name="childPages">Completed child documentation pages</param>
    /// <param name="language">Target language for documentation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Revised parent page with enriched content</returns>
    Task<WikiPage> ReviseParentDocumentationAsync(
        WikiPage parentPage,
        ModuleNode parentModule,
        List<WikiPage> childPages,
        string language = "English",
        CancellationToken cancellationToken = default);
}