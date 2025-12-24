using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition.Strategies;

/// <summary>
/// Strategy that uses Retrieval-Augmented Generation (RAG) for large indexed content.
/// Embeds query, retrieves relevant chunks from vector store, composes focused context.
/// Pure composition - returns messages with retrieved context.
/// </summary>
public class RAGStrategy : IMessageCompositionStrategy
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<RAGStrategy> _logger;
    
    public string StrategyName => "RAG";
    public int Priority => 0; // High priority - preferred over SimpleStrategy (1) when RAG is requested
    
    public RAGStrategy(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILogger<RAGStrategy> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStoreService = vectorStoreService ?? throw new ArgumentNullException(nameof(vectorStoreService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public bool CanHandle(MessageCompositionContext context)
    {
        // RAG requires UseRAG flag and content to be indexed
        if (!context.Metadata.TryGetValue("UseRAG", out var useRAG) 
            || useRAG is not bool useRAGBool 
            || !useRAGBool)
        {
            return false;
        }
        
        // Must have collection name for vector search
        if (!context.Metadata.ContainsKey("CollectionName"))
        {
            _logger.LogWarning("RAG requested but CollectionName not provided in metadata");
            return false;
        }
        
        // Must have query text
        if (string.IsNullOrWhiteSpace(context.TextToProcess))
        {
            _logger.LogWarning("RAG requested but TextToProcess (query) is empty");
            return false;
        }
        
        return true;
    }
    
    public async Task<List<ChatMessage>> ComposeAsync(
        MessageCompositionContext context,
        CancellationToken cancellationToken = default)
    {
        var collectionName = context.Metadata["CollectionName"]?.ToString() 
            ?? throw new InvalidOperationException("CollectionName required for RAG");
        
        // Get optional parameters
        var topK = 5;
        if (context.Metadata.TryGetValue("TopK", out var topKObj) && topKObj is int topKInt)
        {
            topK = topKInt;
        }
        
        Dictionary<string, object>? filters = null;
        if (context.Metadata.TryGetValue("Filters", out var filtersObj) 
            && filtersObj is Dictionary<string, object> filterDict)
        {
            filters = filterDict;
        }
        
        _logger.LogInformation(
            "RAGStrategy retrieving content from collection '{Collection}' (topK: {TopK})",
            collectionName,
            topK);
        
        // Step 1: Embed the query
        var queryText = context.TextToProcess ?? "";
        var queryVector = await _embeddingService.GetEmbeddingAsync(queryText);
        
        // Step 2: Search vector store for relevant chunks
        var results = await _vectorStoreService.SearchAsync(
            collectionName,
            queryVector,
            topK,
            filters);
        
        _logger.LogInformation(
            "RAG retrieved {ResultCount} relevant chunks",
            results.Count);
        
        // Step 3: Build context from retrieved chunks
        var contextBuilder = new System.Text.StringBuilder();
        var sources = new List<object>();

        for (int i = 0; i < results.Count; i++)
        {
            var doc = results[i].Document;
            var title = doc.Metadata.GetValueOrDefault("pageTitle")?.ToString() 
                        ?? doc.Metadata.GetValueOrDefault("filePath")?.ToString() 
                        ?? "Unknown Source";
            
            contextBuilder.AppendLine($"Source [{i + 1}]: {title}");
            contextBuilder.AppendLine(doc.Text);
            contextBuilder.AppendLine("---");
            
            sources.Add(doc);
        }
        
        var retrievedContext = contextBuilder.ToString();
        
        // Add sources to metadata so they can be returned to client
        context.Metadata["RAGSources"] = sources;
        
        // Step 4: Compose messages with retrieved context
        var messages = new List<ChatMessage>();
        
        if (!string.IsNullOrWhiteSpace(context.SystemPrompt))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = context.SystemPrompt
            });
        }
        
        // Add history if any
        if (context.History?.Any() == true)
        {
            messages.AddRange(context.History);
        }
        
        // Add user message with retrieved context + original query
        var userPrompt = $@"Relevant context from documentation:

{retrievedContext}

---

Query: {queryText}

Please respond based on the provided context. When referencing information from the context, please cite the source number using the format [[number]] (e.g., [[1]], [[2]]). Do not use parentheses or other formats, use double brackets.";
        
        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userPrompt
        });
        
        _logger.LogInformation(
            "RAGStrategy composed {MessageCount} messages with {ContextLength} chars of retrieved context",
            messages.Count,
            retrievedContext.Length);
        
        return messages;
    }
}
