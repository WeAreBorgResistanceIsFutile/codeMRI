using codeMRI.Core.Interfaces;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILLMClient _llmClient;

    public ChatController(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // 1. Map DTO to Domain Models
        var history = request.History.Select(m => new codeMRI.Core.Interfaces.ChatMessage(m.Role, m.Content)).ToList();

        // 2. Add current user message to local history copy (API expects complete history usually for chat completion)
        // Check if the last message in history is the user prompt, or if strictly separated.
        // Assuming the frontend sends the *entire* history including the latest user message as the last item.
        // However, ILLMClient.ChatAsync signature is: (system, user, history).
        // So we need to extract the latest user message.
        
        var systemPrompt = "You are a helpful coding assistant integrated into CodeWiki. You help users understand the codebase.";
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

        // 3. Call LLM
        var response = await _llmClient.ChatAsync(systemPrompt, userPrompt, history);

        return Ok(response);
    }
}
