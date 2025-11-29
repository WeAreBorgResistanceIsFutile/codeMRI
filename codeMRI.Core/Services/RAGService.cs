using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Services;

public class RAGService
{
    private readonly ILLMClient _llmClient;
    private readonly IEmbedder _embedder;
    private readonly IVectorDatabase _vectorDb;

    public RAGService(ILLMClient llmClient, IEmbedder embedder, IVectorDatabase vectorDb)
    {
        _llmClient = llmClient;
        _embedder = embedder;
        _vectorDb = vectorDb;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string userQuery, List<ChatMessage> history, string language = "English")
    {
        // 1. Embed query
        var queryEmbedding = await _embedder.EmbedAsync(userQuery);

        // 2. Retrieve relevant docs
        var relevantDocs = await _vectorDb.SearchAsync(queryEmbedding, topK: 5);

        // 3. Construct Context string
        var contextStr = string.Join("\n\n", relevantDocs.Select((d, i) => $"""
            {i+1}. File Path: {d.FilePath}
            Content:
            {d.Content}
            """));

        // 4. Construct System Prompt
        var systemPrompt = $"""
            {PromptTemplates.RAGSystemPrompt(language)}

            <START_OF_CONTEXT>
            {contextStr}
            <END_OF_CONTEXT>
            """;

        // 5. Stream response
        await foreach (var chunk in _llmClient.ChatStreamAsync(systemPrompt, userQuery, history))
        {
            yield return chunk;
        }
    }
}
