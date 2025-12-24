using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition.Strategies;

/// <summary>
/// Strategy that uses Map-Reduce pattern for hierarchical data.
/// Maps each item to a summary, then reduces summaries into final result.
/// Pure composition - returns messages for final reduce step.
/// </summary>
public class MapReduceStrategy : IMessageCompositionStrategy
{
    private readonly ILogger<MapReduceStrategy> _logger;
    
    public string StrategyName => "MapReduce";
    public int Priority => 3; // Mid priority - after Simple and RAG
    
    public MapReduceStrategy(ILogger<MapReduceStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public bool CanHandle(MessageCompositionContext context)
    {
        // MapReduce is for hierarchical/multi-item data
        // Check metadata for UseMapReduce flag and HierarchicalData
        if (!context.Metadata.TryGetValue("UseMapReduce", out var useMapReduce) 
            || useMapReduce is not bool useMapReduceBool 
            || !useMapReduceBool)
        {
            return false;
        }
        
        // Must have hierarchical data provided
        if (!context.Metadata.ContainsKey("HierarchicalData"))
        {
            _logger.LogWarning("MapReduce requested but HierarchicalData not provided in metadata");
            return false;
        }
        
        return true;
    }
    
    public Task<List<ChatMessage>> ComposeAsync(
        MessageCompositionContext context,
        CancellationToken cancellationToken = default)
    {
        // For MapReduce, the caller should have already performed the MAP phase
        // (summarizing each item) and provided the summaries in TextToProcess
        // We just compose the final REDUCE message
        
        var messages = new List<ChatMessage>();
        
        if (!string.IsNullOrWhiteSpace(context.SystemPrompt))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = context.SystemPrompt
            });
        }
        
        // Add history if any
        if (context.History?.Any() == true)
        {
            messages.AddRange(context.History);
        }
        
        // The TextToProcess should contain the summarized/reduced content
        if (!string.IsNullOrWhiteSpace(context.TextToProcess))
        {
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = context.TextToProcess
            });
        }
        
        _logger.LogInformation(
            "MapReduceStrategy composed {MessageCount} messages for final reduce phase",
            messages.Count);
        
        return Task.FromResult(messages);
    }
}
