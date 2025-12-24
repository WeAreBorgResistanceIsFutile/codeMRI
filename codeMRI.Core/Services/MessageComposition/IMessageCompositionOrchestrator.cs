using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Orchestrator that selects and uses the appropriate message composition strategy.
/// ONLY composes messages - does not send them to the LLM.
/// Uses ILLMValidator for size checks during strategy selection.
/// </summary>
public interface IMessageCompositionOrchestrator
{
    /// <summary>
    /// Intelligently composes messages using the best available strategy.
    /// Returns the composed messages without sending them to the LLM.
    /// </summary>
    /// <param name="context">The message composition context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Composed messages ready to send, plus metadata about composition.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no suitable strategy can handle the context.
    /// </exception>
    Task<CompositionResult> ComposeMessagesAsync(
        MessageCompositionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of message composition (without LLM execution).
/// </summary>
public class CompositionResult
{
    /// <summary>
    /// Composed messages ready to send to LLM.
    /// </summary>
    public required List<ChatMessage> Messages { get; init; }
    
    /// <summary>
    /// Name of the strategy that was used.
    /// </summary>
    public required string StrategyUsed { get; init; }
    
    /// <summary>
    /// Additional metadata from the composition process.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

