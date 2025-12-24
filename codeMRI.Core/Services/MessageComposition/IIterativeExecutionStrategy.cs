using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Strategy interface for iterative execution with multiple LLM calls.
/// Unlike IMessageCompositionStrategy, these strategies execute directly with access to ILLMClient.
/// Used for strategies that need iterative processing (chunking, multi-pass reduction).
/// </summary>
public interface IIterativeExecutionStrategy
{
    /// <summary>
    /// Determines if this strategy can handle the given context.
    /// </summary>
    /// <param name="context">The message composition context.</param>
    /// <returns>True if this strategy can handle the context.</returns>
    bool CanHandle(MessageCompositionContext context);
    
    /// <summary>
    /// Executes the strategy with direct access to the LLM client.
    /// This allows for iterative processing where each LLM call depends on the previous response.
    /// </summary>
    /// <param name="context">The message composition context.</param>
    /// <param name="llmClient">LLM client for making requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final synthesized result after all iterations.</returns>
    Task<IterativeExecutionResult> ExecuteAsync(
        MessageCompositionContext context,
        ILLMClient llmClient,
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

/// <summary>
/// Result of iterative execution strategy.
/// </summary>
public class IterativeExecutionResult
{
    /// <summary>
    /// Final synthesized response after all iterations.
    /// </summary>
    public required string FinalResponse { get; init; }
    
    /// <summary>
    /// Number of iterations/chunks processed.
    /// </summary>
    public int IterationsProcessed { get; init; } = 1;
    
    /// <summary>
    /// Additional metadata from the execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
