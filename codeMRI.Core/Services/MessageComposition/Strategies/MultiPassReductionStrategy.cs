using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition.Strategies;

/// <summary>
/// Strategy that iteratively reduces large content through multiple LLM passes.
/// Used for content that can't be semantically chunked (e.g., very long chat history).
/// Implements iterative execution with progressive reduction until content fits.
/// </summary>
public class MultiPassReductionStrategy : IIterativeExecutionStrategy
{
    private readonly ILogger<MultiPassReductionStrategy> _logger;
    
    public string StrategyName => "MultiPassReduction";
    public int Priority => 5; // Lowest priority - fallback strategy
    
    public MultiPassReductionStrategy(ILogger<MultiPassReductionStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public bool CanHandle(MessageCompositionContext context)
    {
        // This is a fallback strategy for large non-chunkable content
        // Typically used for long chat history
        
        // Check if explicitly requested
        if (context.Metadata.TryGetValue("UseMultiPassReduction", out var useMultiPass)
            && useMultiPass is bool useMultiPassBool
            && useMultiPassBool)
        {
            return true;
        }
        
        // Auto-detect: Large history that can't be handled by simple strategy
        if (context.History?.Count > 10)
        {
            var historyTokens = context.History.Sum(m => 
                context.Validator.EstimateTokenCount(m.Content ?? ""));
            var availableTokens = context.Validator.ContextSize - context.Validator.ResponseBuffer;
            
            return historyTokens > availableTokens;
        }
        
        return false;
    }
    
    public async Task<IterativeExecutionResult> ExecuteAsync(
        MessageCompositionContext context,
        ILLMClient llmClient,
        CancellationToken cancellationToken = default)
    {
        if (context.History?.Any() != true)
        {
            throw new InvalidOperationException(
                "MultiPassReductionStrategy requires non-empty History");
        }
        
        _logger.LogInformation(
            "MultiPassReductionStrategy reducing {MessageCount} history messages",
            context.History.Count);
        
        var reducedHistory = new List<ChatMessage>(context.History);
        var passNumber = 0;
        var maxPasses = 5; // Prevent infinite loops
        
        // Iteratively reduce until history fits in context
        while (!HistoryFitsInContext(reducedHistory, context) && passNumber < maxPasses)
        {
            passNumber++;
            
            _logger.LogInformation(
                "MultiPassReduction pass {PassNumber}: reducing {MessageCount} messages",
                passNumber,
                reducedHistory.Count);
            
            reducedHistory = await ReduceHistoryPass(
                reducedHistory,
                context,
                llmClient,
                cancellationToken);
        }
        
        if (!HistoryFitsInContext(reducedHistory, context))
        {
            _logger.LogWarning(
                "MultiPassReduction failed to fit history in context after {MaxPasses} passes. " +
                "Truncating to most recent messages.",
                maxPasses);
            
            // Emergency truncation: keep only most recent messages
            var maxMessages = Math.Min(reducedHistory.Count, 5);
            reducedHistory = reducedHistory.TakeLast(maxMessages).ToList();
        }
        
        // Now compose final message with reduced history
        var messages = new List<ChatMessage>();
        
        if (!string.IsNullOrWhiteSpace(context.SystemPrompt))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = context.SystemPrompt
            });
        }
        
        messages.AddRange(reducedHistory);
        
        if (!string.IsNullOrWhiteSpace(context.TextToProcess))
        {
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = context.TextToProcess
            });
        }
        
        // Execute final request with reduced history
        var finalResponse = await llmClient.ChatAsync(messages, context.Model, cancellationToken);
        
        _logger.LogInformation(
            "MultiPassReduction completed after {PassCount} passes, final history: {FinalMessageCount} messages",
            passNumber,
            reducedHistory.Count);
        
        return new IterativeExecutionResult
        {
            FinalResponse = finalResponse,
            IterationsProcessed = passNumber,
            Metadata = new Dictionary<string, object>
            {
                ["OriginalHistoryCount"] = context.History.Count,
                ["ReducedHistoryCount"] = reducedHistory.Count,
                ["PassesUsed"] = passNumber
            }
        };
    }
    
    private bool HistoryFitsInContext(
        List<ChatMessage> history,
        MessageCompositionContext context)
    {
        var historyTokens = history.Sum(m => 
            context.Validator.EstimateTokenCount(m.Content ?? ""));
        var systemTokens = context.Validator.EstimateTokenCount(context.SystemPrompt ?? "");
        var textTokens = context.Validator.EstimateTokenCount(context.TextToProcess ?? "");
        
        var totalTokens = historyTokens + systemTokens + textTokens;
        var availableTokens = context.Validator.ContextSize - context.Validator.ResponseBuffer;
        
        return totalTokens < availableTokens;
    }
    
    private async Task<List<ChatMessage>> ReduceHistoryPass(
        List<ChatMessage> history,
        MessageCompositionContext context,
        ILLMClient llmClient,
        CancellationToken cancellationToken)
    {
        // Group messages into batches and summarize each batch
        var batchSize = Math.Max(3, history.Count / 3); // Reduce to ~1/3 each pass
        var reducedMessages = new List<ChatMessage>();
        
        for (int i = 0; i < history.Count; i += batchSize)
        {
            var batch = history.Skip(i).Take(batchSize).ToList();
            
            // Keep the very last message of the batch (most recent) verbatim
            if (i + batchSize >= history.Count && batch.Any())
            {
                reducedMessages.AddRange(batch);
                break;
            }
            
            // Summarize this batch
            var batchContent = string.Join("\n\n",
                batch.Select(m => $"{m.Role}: {m.Content}"));
            
            var summaryPrompt = $@"Summarize the following conversation excerpt concisely while preserving key information:

{batchContent}

Provide a brief summary (2-3 sentences):";
            
            var summaryMessages = new List<ChatMessage>
            {
                new() 
                { 
                    Role = "system", 
                    Content = "You are a conversation summarizer. Be extremely concise." 
                },
                new() { Role = "user", Content = summaryPrompt }
            };
            
            var summary = await llmClient.ChatAsync(summaryMessages, context.Model, cancellationToken);
            
            // Add summarized batch as a single message
            reducedMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = $"[Summary of {batch.Count} messages]: {summary}"
            });
        }
        
        return reducedMessages;
    }
}
