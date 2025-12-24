using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Orchestrator implementation that selects and uses message composition strategies.
/// Handles both pure composition strategies and iterative execution strategies.
/// </summary>
public class MessageCompositionOrchestrator : IMessageCompositionOrchestrator
{
    private readonly IEnumerable<IMessageCompositionStrategy> _compositionStrategies;
    private readonly IEnumerable<IIterativeExecutionStrategy> _iterativeStrategies;
    private readonly ILogger<MessageCompositionOrchestrator> _logger;
    
    public MessageCompositionOrchestrator(
        IEnumerable<IMessageCompositionStrategy> compositionStrategies,
        IEnumerable<IIterativeExecutionStrategy> iterativeStrategies,
        ILogger<MessageCompositionOrchestrator> logger)
    {
        _compositionStrategies = compositionStrategies ?? throw new ArgumentNullException(nameof(compositionStrategies));
        _iterativeStrategies = iterativeStrategies ?? throw new ArgumentNullException(nameof(iterativeStrategies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<CompositionResult> ComposeMessagesAsync(
        MessageCompositionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        
        // Check iterative strategies first (they have specific use cases)
        var iterativeCandidate = _iterativeStrategies
            .Where(s => s.CanHandle(context))
            .OrderBy(s => s.Priority)
            .FirstOrDefault();
        
        if (iterativeCandidate != null)
        {
            _logger.LogInformation(
                "Selected iterative execution strategy: {StrategyName} (priority {Priority})",
                iterativeCandidate.StrategyName,
                iterativeCandidate.Priority);
            
            // Iterative strategies need special handling - return marker for facade
            return new CompositionResult
            {
                Messages = new List<ChatMessage>(), // Empty - will be executed by strategy
                StrategyUsed = iterativeCandidate.StrategyName,
                Metadata = new Dictionary<string, object>
                {
                    ["IsIterativeStrategy"] = true,
                    ["Strategy"] = iterativeCandidate
                }
            };
        }
        
        // Find composition strategies
        var compositionCandidates = _compositionStrategies
            .Where(s => s.CanHandle(context))
            .OrderBy(s => s.Priority)
            .ToList();
        
        if (!compositionCandidates.Any())
        {
            var contextInfo = $"SystemPrompt: {context.SystemPrompt?.Length ?? 0} chars, " +
                            $"TextToProcess: {context.TextToProcess?.Length ?? 0} chars, " +
                            $"History: {context.History?.Count ?? 0} messages, " +
                            $"ContextSize: {context.Validator.ContextSize}";
            
            _logger.LogError(
                "No suitable strategy found for context. {ContextInfo}",
                contextInfo);
            
            throw new InvalidOperationException(
                $"No suitable strategy found for the given context. {contextInfo}");
        }
        
        var selectedStrategy = compositionCandidates.First();
        
        _logger.LogInformation(
            "Selected composition strategy: {StrategyName} (priority {Priority}) from {CandidateCount} candidates",
            selectedStrategy.StrategyName,
            selectedStrategy.Priority,
            compositionCandidates.Count);
        
        if (compositionCandidates.Count > 1)
        {
            var alternatives = string.Join(", ", 
                compositionCandidates.Skip(1).Select(s => s.StrategyName));
            _logger.LogDebug(
                "Alternative composition strategies available: {Alternatives}",
                alternatives);
        }
        
        try
        {
            var messages = await selectedStrategy.ComposeAsync(context, cancellationToken);
            
            _logger.LogInformation(
                "Strategy {StrategyName} composed {MessageCount} messages successfully",
                selectedStrategy.StrategyName,
                messages.Count);
            
            return new CompositionResult
            {
                Messages = messages,
                StrategyUsed = selectedStrategy.StrategyName,
                Metadata = new Dictionary<string, object>(context.Metadata)
                {
                    ["MessageCount"] = messages.Count,
                    ["Priority"] = selectedStrategy.Priority,
                    ["IsIterativeStrategy"] = false
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Strategy {StrategyName} failed during composition",
                selectedStrategy.StrategyName);
            throw;
        }
    }
}

