using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.Decorators;

/// <summary>
///     Decorator for ILLMClient that captures failing context as a DebugSnapshot
/// </summary>
public class DebugSnapshotLLMClientDecorator : ILLMClient
{
    private readonly ILLMClient _inner;
    private readonly IDebugSnapshotService _snapshotService;
    private readonly ILLMInvocationContext _invocationContext;
    private readonly ILLMValidator _validator;
    private readonly ILogger<DebugSnapshotLLMClientDecorator> _logger;

    public DebugSnapshotLLMClientDecorator(
        ILLMClient inner,
        IDebugSnapshotService snapshotService,
        ILLMInvocationContext invocationContext,
        ILLMValidator validator,
        ILogger<DebugSnapshotLLMClientDecorator> logger)
    {
        _inner = inner;
        _snapshotService = snapshotService;
        _invocationContext = invocationContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<string> ChatAsync(List<ChatMessage> messages, string? model = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _inner.ChatAsync(messages, model, cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(messages, model, ex);
            throw; // Re-throw to maintain original behavior
        }
    }

    private async Task HandleFailureAsync(List<ChatMessage> messages, string? model, Exception ex)
    {
        try
        {
            var repoPath = _invocationContext.RepoPath;
            if (string.IsNullOrEmpty(repoPath))
            {
                _logger.LogWarning("Cannot save debug snapshot: RepoPath is missing in invocation context.");
                return;
            }

            var systemPrompt = messages.FirstOrDefault(m => m.Role == "system")?.Content ?? string.Empty;
            var userPrompt = messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

            var validation = _validator.ValidateMessages(messages);

            var snapshot = new DebugSnapshot
            {
                JobId = _invocationContext.JobId ?? "unknown",
                ComponentId = _invocationContext.ComponentId ?? "unknown",
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                Model = model ?? "default",
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["tokenCount"] = validation.EstimatedTokens.ToString()
                }
            };

            var path = await _snapshotService.SaveSnapshotAsync(repoPath, snapshot);
            _logger.LogInformation("Saved failing LLM context to debug snapshot: {Path}", path);
        }
        catch (Exception snapshotEx)
        {
            _logger.LogError(snapshotEx, "Failed to save debug snapshot during LLM failure handling.");
        }
    }
}
