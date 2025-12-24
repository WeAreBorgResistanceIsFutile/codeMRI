using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition.Strategies;

/// <summary>
/// Simple strategy that composes messages directly when everything fits in context.
/// Does NOT send to LLM - only composes the message list.
/// </summary>
public class SimpleMessageStrategy : IMessageCompositionStrategy
{
    private readonly ILogger<SimpleMessageStrategy> _logger;
    
    public string StrategyName => "Simple";
    public int Priority => 1; // Highest priority - try this first
    
    public SimpleMessageStrategy(ILogger<SimpleMessageStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public bool CanHandle(MessageCompositionContext context)
    {
        // Estimate total size using validator
        var systemLen = context.SystemPrompt?.Length ?? 0;
        var textLen = context.TextToProcess?.Length ?? 0;
        var historyLen = context.History?.Sum(h => h.Content?.Length ?? 0) ?? 0;
        
        var totalChars = systemLen + textLen + historyLen;
        var estimatedTokens = context.Validator.EstimateTokenCount(totalChars.ToString());
        var availableTokens = context.Validator.ContextSize - context.Validator.ResponseBuffer;
        
        var canHandle = estimatedTokens < availableTokens;
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "SimpleMessageStrategy.CanHandle: {CanHandle} (estimated {EstimatedTokens} tokens, available {AvailableTokens} tokens)",
                canHandle, estimatedTokens, availableTokens);
        }
        
        return canHandle;
    }
    
    public Task<List<ChatMessage>> ComposeAsync(
        MessageCompositionContext context, 
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();
        
        // Add system prompt
        if (!string.IsNullOrWhiteSpace(context.SystemPrompt))
        {
            messages.Add(new ChatMessage 
            { 
                Role = "system", 
                Content = context.SystemPrompt 
            });
        }
        
        // Add history
        if (context.History != null)
        {
            messages.AddRange(context.History);
        }
        
        // Add text to process as user message
        if (!string.IsNullOrWhiteSpace(context.TextToProcess))
        {
            messages.Add(new ChatMessage 
            { 
                Role = "user", 
                Content = context.TextToProcess 
            });
        }
        
        _logger.LogInformation(
            "SimpleMessageStrategy composed {MessageCount} messages (total ~{TotalChars} chars)",
            messages.Count,
            messages.Sum(m => m.Content?.Length ?? 0));
        
        return Task.FromResult(messages);
    }
}

