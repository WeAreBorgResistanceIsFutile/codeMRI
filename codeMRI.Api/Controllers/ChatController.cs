using codeMRI.Core.Services;
using codeMRI.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace codeMRI.Api.Controllers;

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
        await foreach (var chunk in _ragService.ChatStreamAsync(request.Query, request.History, request.Language))
        {
            await Response.WriteAsync(chunk);
            await Response.Body.FlushAsync();
        }
    }
}