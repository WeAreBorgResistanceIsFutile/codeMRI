using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Strategy interface for composing messages.
/// Strategies ONLY compose messages - they do not send them to the LLM.
/// </summary>
public interface IMessageCompositionStrategy
{
    /// <summary>
    /// Determines if this strategy can handle the given context.
    /// </summary>
    /// <param name="context">The message composition context.</param>
    /// <returns>True if this strategy can handle the context.</returns>
    bool CanHandle(MessageCompositionContext context);
    
    /// <summary>
    /// Composes messages using this strategy.
    /// Returns the composed messages without sending them to the LLM.
    /// </summary>
    /// <param name="context">The message composition context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Composed messages ready to send to LLM.</returns>
    Task<List<ChatMessage>> ComposeAsync(
        MessageCompositionContext context, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Priority of this strategy (lower number = higher priority).
    /// Used for ordering when multiple strategies can handle a context.
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Human-readable name of this strategy.
    /// </summary>
    string StrategyName { get; }
}

