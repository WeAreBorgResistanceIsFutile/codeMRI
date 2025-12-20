using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
/// Service for hierarchical summarization and entity extraction.
/// Implements Map-Reduce pattern for large documentation processing.
/// </summary>
public interface IHierarchicalSummaryService
{
    /// <summary>
    /// Extracts key entities (class names, function names, patterns) from a wiki page.
    /// These entities should be preserved during summarization (Entity Anchoring).
    /// </summary>
    /// <param name="page">The wiki page to extract entities from</param>
    /// <returns>Extracted entities</returns>
    ExtractedEntities ExtractKeyEntities(WikiPage page);
    
    /// <summary>
    /// Extracts entities from multiple pages and combines them.
    /// </summary>
    /// <param name="pages">List of wiki pages</param>
    /// <returns>Combined extracted entities (deduplicated)</returns>
    ExtractedEntities ExtractKeyEntities(List<WikiPage> pages);
    
    /// <summary>
    /// Creates a compressed summary from full documentation (Map phase).
    /// </summary>
    /// <param name="page">Full wiki page</param>
    /// <param name="maxWords">Target maximum words (default: 200)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compressed module summary</returns>
    Task<ModuleSummary> SummarizeModuleAsync(
        WikiPage page, 
        int maxWords = 200,
        CancellationToken cancellationToken = default);
}
