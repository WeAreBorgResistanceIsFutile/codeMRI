using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Facade implementation providing simple API for LLM interactions.
/// Coordinates message composition (via orchestrator) and sending (via LLM client).
/// Handles both composition strategies and iterative execution strategies.
/// </summary>
public class LLMServiceFacade : ILLMServiceFacade
{
    private readonly IMessageCompositionOrchestrator _orchestrator;
    private readonly ILLMClient _llmClient;
    private readonly ILLMValidator _validator;
    private readonly ILogger<LLMServiceFacade> _logger;
    
    public LLMServiceFacade(
        IMessageCompositionOrchestrator orchestrator,
        ILLMClient llmClient,
        ILLMValidator validator,
        ILogger<LLMServiceFacade> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<LLMResponse> ExecuteAsync(
        string systemPrompt,
        string textToProcess,
        List<ChatMessage>? history = null,
        MessageCompositionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "LLMServiceFacade executing request (system: {SystemLen} chars, text: {TextLen} chars, history: {HistoryCount} messages)",
            systemPrompt?.Length ?? 0,
            textToProcess?.Length ?? 0,
            history?.Count ?? 0);
        
        // Build composition context
        var context = new MessageCompositionContext
        {
            SystemPrompt = systemPrompt,
            TextToProcess = textToProcess,
            History = history ?? new List<ChatMessage>(),
            Validator = _validator,
            Model = options?.ModelName,
            Metadata = options?.CustomOptions ?? new Dictionary<string, object>()
        };
        
        // Ensure some options are passed through metadata if needed by strategies
        if (options != null)
        {
            context.Metadata["UseSemanticChunking"] = options.UseSemanticChunking;
            context.Metadata["UseRag"] = options.UseRag;
        }
        
        try
        {
            // Step 1: Let orchestrator select strategy and compose/prepare
            var compositionResult = await _orchestrator.ComposeMessagesAsync(context, cancellationToken);
            
            // Check if it's an iterative strategy
            if (compositionResult.Metadata.TryGetValue("IsIterativeStrategy", out var isIterative)
                && isIterative is bool iterativeBool && iterativeBool)
            {
                // Execute iterative strategy
                var strategy = compositionResult.Metadata["Strategy"] as IIterativeExecutionStrategy;
                if (strategy == null)
                    throw new InvalidOperationException("Iterative strategy marker found but strategy is null");
                
                _logger.LogInformation(
                    "Executing iterative strategy: {Strategy}",
                    compositionResult.StrategyUsed);
                
                var iterativeResult = await strategy.ExecuteAsync(context, _llmClient, cancellationToken);
                
                _logger.LogInformation(
                    "Iterative strategy completed ({Iterations} iterations, response length: {ResponseLength} chars)",
                    iterativeResult.IterationsProcessed,
                    iterativeResult.FinalResponse?.Length ?? 0);
                
                return new LLMResponse
                {
                    Content = iterativeResult.FinalResponse,
                    StrategyUsed = compositionResult.StrategyUsed,
                    ChunksProcessed = iterativeResult.IterationsProcessed,
                    Metadata = iterativeResult.Metadata
                };
            }
            else
            {
                // Composition strategy - send composed messages to LLM
                _logger.LogInformation(
                    "Messages composed using {Strategy} strategy ({MessageCount} messages)",
                    compositionResult.StrategyUsed,
                    compositionResult.Messages.Count);
                
                var response = await _llmClient.ChatAsync(
                    compositionResult.Messages,
                    context.Model,
                    cancellationToken);
                
                _logger.LogInformation(
                    "LLM responded successfully (response length: {ResponseLength} chars)",
                    response?.Length ?? 0);
                
                return new LLMResponse
                {
                    Content = response,
                    StrategyUsed = compositionResult.StrategyUsed,
                    ChunksProcessed = 1,
                    Metadata = compositionResult.Metadata
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLMServiceFacade execution failed");
            throw;
        }
    }
}

