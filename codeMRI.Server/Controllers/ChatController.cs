using codeMRI.Core.Services;
using codeMRI.Server.Api;
using Microsoft.AspNetCore.Mvc;
using ChatMessage = codeMRI.Core.Interfaces.ChatMessage;

namespace codeMRI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly RAGService _ragService;

    public ChatController(RAGService ragService)
    {
        _ragService = ragService;
    }

    [HttpPost]
    public async Task Chat([FromBody] ChatRequest request)
    {
        Response.ContentType = "text/plain";
        await foreach (var chunk in _ragService.ChatStreamAsync(request.Query, MapChatMessages(request.History),
                           request.Language))
        {
            await Response.WriteAsync(chunk);
            await Response.Body.FlushAsync();
        }
    }

    private static List<ChatMessage> MapChatMessages(List<Api.ChatMessage> messages)
    {
        return messages.Select(p => new ChatMessage(p.Role, p.Content)).ToList();
    }
}