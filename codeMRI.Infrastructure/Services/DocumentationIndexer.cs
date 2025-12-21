using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using Microsoft.Extensions.Logging;

namespace codeMRI.Infrastructure.Services;

public class DocumentationIndexer : IDocumentIndexer
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly SemanticDocumentChunker _chunker;
    private readonly ILogger<DocumentationIndexer> _logger;
    private const string DocumentationCollection = "documentation";

    public DocumentationIndexer(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILogger<DocumentationIndexer> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
        _chunker = new SemanticDocumentChunker();
    }

    public async Task IndexDocumentationAsync(string repoPath, WikiStructure structure, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Indexing documentation for repository: {RepoPath}", repoPath);
        
        await _vectorStoreService.CreateCollectionAsync(DocumentationCollection, _embeddingService.GetDimensions());

        foreach (var page in structure.Pages)
        {
            await IndexPageAsync(repoPath, page, cancellationToken);
        }
    }

    public async Task IndexPageAsync(string repoPath, WikiPage page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(page.Content)) return;

        var chunks = _chunker.ChunkMarkdown(page.Content);
        _logger.LogDebug("Indexing page {PageTitle} ({ChunkCount} chunks)", page.Title, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var embedding = await _embeddingService.GetEmbeddingAsync(chunkText);
            
            var doc = new VectorDocument
            {
                Text = chunkText,
                Vector = embedding,
                Metadata = new Dictionary<string, object>
                {
                    { "repoPath", repoPath },
                    { "pageId", page.Id },
                    { "pageTitle", page.Title },
                    { "chunkIndex", i },
                    { "documentType", "documentation" }
                }
            };

            await _vectorStoreService.UpsertAsync(DocumentationCollection, doc);
        }
    }

    public async Task IndexCodebaseAsync(string repoPath, EnhancedDependencyGraph graph, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Indexing codebase for repository: {RepoPath}", repoPath);
        
        await _vectorStoreService.CreateCollectionAsync("code", _embeddingService.GetDimensions());
        var codeChunker = new CodeChunker();

        foreach (var node in graph.GetNodes())
        {
            var chunks = codeChunker.ChunkCode(node);
            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(chunk.Text);
                var doc = new VectorDocument
                {
                    Text = chunk.Text,
                    Vector = embedding,
                    Metadata = new Dictionary<string, object>
                    {
                        { "repoPath", repoPath },
                        { "filePath", chunk.FilePath },
                        { "componentId", chunk.ComponentId },
                        { "componentType", chunk.Type },
                        { "startLine", chunk.StartLine },
                        { "endLine", chunk.EndLine },
                        { "documentType", "code" }
                    }
                };
                await _vectorStoreService.UpsertAsync("code", doc);
            }
        }
    }

    public async Task DeleteIndexAsync(string repoPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting vector index for repository: {RepoPath}", repoPath);
        
        var filter = new Dictionary<string, object> { { "repoPath", repoPath } };
        
        await _vectorStoreService.DeleteByFilterAsync(DocumentationCollection, filter);
        await _vectorStoreService.DeleteByFilterAsync("code", filter);
    }
}
