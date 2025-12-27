using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.Decorators;

/// <summary>
///     Decorator for IEmbeddingService that captures failing context as a DebugSnapshot
/// </summary>
public class DebugSnapshotEmbeddingServiceDecorator : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly IDebugSnapshotService _snapshotService;
    private readonly ILLMInvocationContext _invocationContext;
    private readonly ILLMValidator _validator;
    private readonly ILogger<DebugSnapshotEmbeddingServiceDecorator> _logger;

    public DebugSnapshotEmbeddingServiceDecorator(
        IEmbeddingService inner,
        IDebugSnapshotService snapshotService,
        ILLMInvocationContext invocationContext,
        ILLMValidator validator,
        ILogger<DebugSnapshotEmbeddingServiceDecorator> logger)
    {
        _inner = inner;
        _snapshotService = snapshotService;
        _invocationContext = invocationContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        try
        {
            return await _inner.GetEmbeddingAsync(text);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(text, ex);
            throw; // Re-throw to maintain original behavior
        }
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
    {
        try
        {
            return await _inner.GetEmbeddingsAsync(texts);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(string.Join(", ", texts.Take(3)) + (texts.Count > 3 ? "..." : ""), ex);
            throw; // Re-throw to maintain original behavior
        }
    }

    public int GetDimensions()
    {
        return _inner.GetDimensions();
    }

    public string ModelName => _inner.ModelName;

    private async Task HandleFailureAsync(string text, Exception ex)
    {
        try
        {
            var repoPath = _invocationContext.RepoPath;
            if (string.IsNullOrEmpty(repoPath))
            {
                _logger.LogWarning("Cannot save debug snapshot: RepoPath is missing in invocation context.");
                return;
            }

            var snapshot = new DebugSnapshot
            {
                JobId = _invocationContext.JobId ?? "unknown",
                ComponentId = _invocationContext.ComponentId ?? "unknown",
                SystemPrompt = "Embedding Request",
                UserPrompt = text,
                Model = ModelName,
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["embeddingText"] = text,  // Full text, no truncation
                    ["tokenCount"] = _validator.EstimateTokenCount(text).ToString()
                }
            };

            var path = await _snapshotService.SaveSnapshotAsync(repoPath, snapshot);
            _logger.LogInformation("Saved failing embedding context to debug snapshot: {Path}", path);
        }
        catch (Exception snapshotEx)
        {
            _logger.LogError(snapshotEx, "Failed to save debug snapshot during embedding failure handling.");
        }
    }
}
