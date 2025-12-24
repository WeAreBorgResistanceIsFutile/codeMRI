using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Response from LLM facade containing the result and metadata.
/// </summary>
public class LLMResponse
{
    /// <summary>
    /// The LLM's response content.
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Name of the strategy that was used for composition.
    /// </summary>
    public required string StrategyUsed { get; init; }
    
    /// <summary>
    /// Number of chunks processed (1 for non-chunking strategies).
    /// </summary>
    public int ChunksProcessed { get; init; } = 1;
    
    /// <summary>
    /// Additional metadata from strategy execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Options for message composition and execution.
/// </summary>
public class MessageCompositionOptions
{
    private Dictionary<string, object> _options = new();

    /// <summary>
    /// Optional model name to use (if different from default).
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Whether to use semantic chunking if content is large.
    /// </summary>
    public bool UseSemanticChunking { get; init; } = true;

    /// <summary>
    /// Whether to use RAG strategy if content is large.
    /// </summary>
    public bool UseRag { get; init; }

    /// <summary>
    /// Custom metadata or options for strategies.
    /// </summary>
    public Dictionary<string, object> CustomOptions => _options;
}

/// <summary>
/// Facade providing simple API for LLM interactions.
/// Handles message composition via orchestrator and sending via LLM client.
/// </summary>
public interface ILLMServiceFacade
{
    /// <summary>
    /// Execute LLM request with automatic strategy selection and composition.
    /// This is the primary method clients should use for LLM interactions.
    /// </summary>
    /// <param name="systemPrompt">System prompt providing instructions to the LLM.</param>
    /// <param name="textToProcess">Main text content to process (code, documentation, etc.).</param>
    /// <param name="history">Optional conversation history.</param>
    /// <param name="options">Optional strategy-specific options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response with content and metadata.</returns>
    Task<LLMResponse> ExecuteAsync(
        string systemPrompt,
        string textToProcess,
        List<ChatMessage>? history = null,
        MessageCompositionOptions? options = null,
        CancellationToken cancellationToken = default);
}
