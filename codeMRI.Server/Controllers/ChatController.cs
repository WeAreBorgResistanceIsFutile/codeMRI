using codeMRI.Core.Services.MessageComposition;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.AspNetCore.Mvc;
using ChatMessage = codeMRI.Core.Interfaces.ChatMessage;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILLMServiceFacade _llmFacade;

    public ChatController(ILLMServiceFacade llmFacade)
    {
        _llmFacade = llmFacade;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // 1. Map DTO to Domain Models
        var history = request.History.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

        // 2. Add current user message to local history copy (API expects complete history usually for chat completion)
        // Check if the last message in history is the user prompt, or if strictly separated.
        // Assuming the frontend sends the *entire* history including the latest user message as the last item.
        // However, ILLMClient.ChatAsync signature is: (system, user, history).
        // So we need to extract the latest user message.

        var systemPrompt =
            "You are a helpful coding assistant integrated into CodeWiki. You help users understand the codebase.";
        var userPrompt = "";

        if (history.Any() && history.Last().Role == "user")
        {
            userPrompt = history.Last().Content;
            history.RemoveAt(history.Count - 1); // Remove from history as it is passed as 'userPrompt'
        }
        else
        {
            return BadRequest("History must end with a user message.");
        }

        // 3. Call LLM via facade
        MessageCompositionOptions? options = null;

        if (!string.IsNullOrEmpty(request.RepoPath))
        {
            options = new MessageCompositionOptions
            {
                UseRag = true,
                Metadata = new Dictionary<string, object>
                {
                    { "UseRAG", true },
                    { "CollectionName", "documentation" },
                    { "Filters", new Dictionary<string, object> { { "repoPath", request.RepoPath } } }
                }
            };
        }

        // 3. Call LLM via facade
        var llmResponse = await _llmFacade.ExecuteAsync(
            systemPrompt: systemPrompt,
            textToProcess: userPrompt,
            history: history,
            options: options,
            cancellationToken: default);

        var response = new ChatResponse
        {
            Message = llmResponse.Content
        };

        if (llmResponse.Metadata.TryGetValue("RAGSources", out var sourcesObj) && sourcesObj is List<object> sourcesList)
        {
            // Convert VectorDocuments to SourceDocuments
            // We need to cast the objects back to VectorDocument. Since VectorDocument is in Core.Models (or Interfaces?), 
            // and sourcesObj came from RAGStrategy which uses IVectorStoreService -> VectorDocument.
            // But here it might be handled as object.
            
            foreach (var src in sourcesList)
            {
                if (src is VectorDocument doc)
                {
                    response.Sources.Add(new SourceDocument
                    {
                        Id = doc.Id,
                        Title = doc.Metadata.GetValueOrDefault("pageTitle")?.ToString() 
                                ?? doc.Metadata.GetValueOrDefault("filePath")?.ToString() 
                                ?? "Unknown",
                        FilePath = doc.Metadata.GetValueOrDefault("filePath")?.ToString() ?? "",
                        Snippet = doc.Text
                    });
                }
            }
        }

        return Ok(response);
    }
}