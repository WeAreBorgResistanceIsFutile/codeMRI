using codeMRI.Core.Interfaces;
using codeMRI.Core.Utils;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services.MessageComposition.Strategies;

/// <summary>
/// Strategy that processes large content by chunking iteratively with LLM calls.
/// Implements iterative execution with prompt reformulation and result reconstruction.
/// </summary>
public class ChunkingMessageStrategy : IIterativeExecutionStrategy
{
    private readonly ILogger<ChunkingMessageStrategy> _logger;
    
    public string StrategyName => "Chunking";
    public int Priority => 4; // Lower priority - use when simpler strategies can't handle
    
    public ChunkingMessageStrategy(ILogger<ChunkingMessageStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public bool CanHandle(MessageCompositionContext context)
    {
        // This strategy handles large text content that exceeds context window
        if (string.IsNullOrWhiteSpace(context.TextToProcess))
            return false;
        
        // Estimate if content is too large for simple strategy
        var textTokens = context.Validator.EstimateTokenCount(context.TextToProcess);
        var systemTokens = context.Validator.EstimateTokenCount(context.SystemPrompt ?? "");
        var totalTokens = textTokens + systemTokens;
        var availableTokens = context.Validator.ContextSize - context.Validator.ResponseBuffer;
        
        return totalTokens > availableTokens;
    }
    
    public async Task<IterativeExecutionResult> ExecuteAsync(
        MessageCompositionContext context,
        ILLMClient llmClient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.TextToProcess))
        {
            throw new InvalidOperationException(
                "ChunkingMessageStrategy requires TextToProcess to be non-empty");
        }
        
        // Check for semantic chunking preference
        var useSemanticChunking = true;
        if (context.Metadata.TryGetValue("UseSemanticChunking", out var useSemantic)  
            && useSemantic is bool semanticBool)
        {
            useSemanticChunking = semanticBool;
        }
        
        // Calculate available tokens for input (Context - Buffer - Safety Margin)
        var inputSafetyMargin = 200; // Tokens reserved for prompt overhead
        var availableInputTokens = context.Validator.ContextSize 
                                 - context.Validator.ResponseBuffer 
                                 - inputSafetyMargin;
                                 
        if (availableInputTokens <= 0)
        {
             // Fallback if buffer is too large relative to context
             availableInputTokens = (int)(context.Validator.ContextSize * 0.7);
             _logger.LogWarning("Available input tokens calculated as zero or negative. Using 70% of ContextSize as fallback: {FallbackTokens}", availableInputTokens);
        }

        // Calculate chunk size (50% of available input for text)
        var chunkTokens = (int)(availableInputTokens * 0.5);
        var chunkSize = chunkTokens * 4; // Approx 4 chars per token
        var overlap = chunkSize / 10; // 10% overlap
        
        var chunks = useSemanticChunking
            ? TextSplitter.SplitSemantically(context.TextToProcess, chunkSize, overlap)
            : TextSplitter.Split(context.TextToProcess, chunkSize, overlap);
        
        _logger.LogInformation(
            "ChunkingMessageStrategy processing large content ({TotalChars} chars) in {ChunkCount} chunks (semantic: {UseSemanticChunking})",
            context.TextToProcess.Length,
            chunks.Count,
            useSemanticChunking);
        
        // Re formulate system prompt for chunking context
        var chunkSystemPrompt = ReformulateSystemPrompt(context.SystemPrompt);
        
        // Process chunks iteratively
        var iterationFindings = new List<string>();
        var rollingState = string.Empty;
        
        for (var i = 0; i < chunks.Count; i++)
        {
            // Ensure findings don't exceed 30% of available input for the rolling context
            var maxFindingsTokens = (int)(availableInputTokens * 0.3);
            var findingsTokens = context.Validator.EstimateTokenCount(rollingState);
            
            if (findingsTokens > maxFindingsTokens)
            {
                _logger.LogInformation(
                    "Rolling findings context too large ({FindingsTokens} tokens). Compacting to fit within {MaxTokens} tokens",
                    findingsTokens,
                    maxFindingsTokens);
                
                rollingState = await CompactFindingsAsync(rollingState, maxFindingsTokens, llmClient, context.Model, cancellationToken);
            }
            
            // Build chunk prompt
            var chunkPrompt = BuildChunkPrompt(
                context.SystemPrompt ?? "",
                chunks[i],
                i + 1,
                chunks.Count,
                rollingState
            );
            
            var messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = chunkSystemPrompt },
                new() { Role = "user", Content = chunkPrompt }
            };
            
            _logger.LogDebug(
                "Processing chunk {ChunkIndex}/{TotalChunks} ({ChunkSize} chars)",
                i + 1,
                chunks.Count,
                chunks[i].Length);
            
            var response = await llmClient.ChatAsync(messages, context.Model, cancellationToken);
            iterationFindings.Add(response);
            rollingState = response; // For next iteration, our "rolling state" is the latest finding
        }
        
        // RESULT RECONSTRUCTION: Synthesize final coherent result
        _logger.LogInformation("Reconstructing final result from {FindingsCount} iteration findings", 
            iterationFindings.Count);
        
        var finalResult = await SynthesizeFinalResult(
            context.SystemPrompt ?? "",
            iterationFindings,
            llmClient,
            context.Validator,
            context.Model,
            0, // Initial recursion depth
            cancellationToken
        );
        
        return new IterativeExecutionResult
        {
            FinalResponse = finalResult,
            IterationsProcessed = chunks.Count,
            Metadata = new Dictionary<string, object>
            {
                ["UseSemanticChunking"] = useSemanticChunking,
                ["ChunkSize"] = chunkSize,
                ["OriginalTextLength"] = context.TextToProcess.Length,
                ["FindingsCount"] = iterationFindings.Count
            }
        };
    }

    private async Task<string> CompactFindingsAsync(
        string findings,
        int maxTokens,
        ILLMClient llmClient,
        string? model,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new() 
            { 
                Role = "system", 
                Content = "You are an expert at distilling and summarizing information while preserving core insights. " +
                          "Provide a concise summary that captures all key findings and critical details, fitting within the specified token limit."
            },
            new() 
            { 
                Role = "user", 
                Content = $"Summarize these findings to be more concise (target {maxTokens} tokens), while preserving all information needed for subsequent analysis:\n\n{findings}" 
            }
        };

        return await llmClient.ChatAsync(messages, model, cancellationToken);
    }
    
    private string ReformulateSystemPrompt(string? originalSystemPrompt)
    {
        if (string.IsNullOrWhiteSpace(originalSystemPrompt))
            return "You are analyzing large content in chunks.";
        
        // Transform "Summarize this document" 
        // → "You are analyzing large content in chunks to provide a summary."
        return $"You are analyzing large content in chunks. Your task: {originalSystemPrompt}";
    }
    
    private string BuildChunkPrompt(
        string originalIntent,
        string chunkContent,
        int chunkNumber,
        int totalChunks,
        string previousFindings)
    {
        if (string.IsNullOrEmpty(previousFindings))
        {
            // First chunk
            return $@"CHUNK {chunkNumber} of {totalChunks}

Analyze this chunk and provide findings that will be combined with findings from subsequent chunks.

Original task: {originalIntent}

Content:
{chunkContent}

Provide your findings (these will be updated with findings from subsequent chunks):";
        }
        else
        {
            // Subsequent chunks - include previous findings
            return $@"CHUNK {chunkNumber} of {totalChunks}

Previous findings from earlier chunks:
{previousFindings}

Update and expand these findings based on this new chunk.

Content:
{chunkContent}

Provide updated comprehensive findings:";
        }
    }
    
    private async Task<string> SynthesizeFinalResult(
        string originalRequest,
        List<string> iterationFindings,
        ILLMClient llmClient,
        ILLMValidator validator,
        string? model,
        int recursionDepth,
        CancellationToken cancellationToken)
    {
        // Safety guard against infinite recursion
        if (recursionDepth > 5)
        {
            _logger.LogWarning("Hierarchical synthesis reached max recursion depth {Depth}. Forcing final synthesis with potentially large context.", recursionDepth);
            // Optimization: If we have iterationFindings, we join them.
            // But we already do that in combinedFindings below. 
            // We should just proceed to ExecuteSynthesisCall directly below after check.
            // But wait, the check 'findingsTokens <= available' is done first.
            // We should force it.
            
            var forceCombined = string.Join("\n\n---\n\n", iterationFindings);
            return await ExecuteSynthesisCall(originalRequest, forceCombined, llmClient, model, cancellationToken);
        }

        var combinedFindings = string.Join("\n\n---\n\n", iterationFindings);
        var findingsTokens = validator.EstimateTokenCount(combinedFindings);
        
        // Calculate available tokens for the synthesis prompt
        var inputSafetyMargin = 300; // Slightly larger for final synthesis
        var availableInputTokens = validator.ContextSize - validator.ResponseBuffer - inputSafetyMargin;
        
        // If combined findings fit, do it in one call
        if (findingsTokens <= availableInputTokens * 0.8) // Use 80% to be safe
        {
            return await ExecuteSynthesisCall(originalRequest, combinedFindings, llmClient, model, cancellationToken);
        }

        // Hierarchical Synthesis: We need to reduce findings in groups
        _logger.LogInformation("Combined findings ({Tokens} tokens) exceed available synthesis context ({Available} tokens). Performing hierarchical synthesis.", 
            findingsTokens, availableInputTokens);

        var reducedFindings = new List<string>();
        var currentBatch = new List<string>();
        var currentBatchTokens = 0;

        foreach (var finding in iterationFindings)
        {
            var tokenCount = validator.EstimateTokenCount(finding);
            if (currentBatchTokens + tokenCount > availableInputTokens * 0.7 && currentBatch.Any())
            {
                // Process current batch
                var batchString = string.Join("\n\n", currentBatch);
                var summary = await CompactFindingsAsync(batchString, (int)(availableInputTokens * 0.4), llmClient, model, cancellationToken);
                reducedFindings.Add(summary);
                
                currentBatch.Clear();
                currentBatchTokens = 0;
            }
            
            currentBatch.Add(finding);
            currentBatchTokens += tokenCount;
        }

        if (currentBatch.Any())
        {
            var batchString = string.Join("\n\n", currentBatch);
            var summary = await CompactFindingsAsync(batchString, (int)(availableInputTokens * 0.4), llmClient, model, cancellationToken);
            reducedFindings.Add(summary);
        }

        // Final recursive call with reduced findings
        return await SynthesizeFinalResult(originalRequest, reducedFindings, llmClient, validator, model, recursionDepth + 1, cancellationToken);
    }

    private async Task<string> ExecuteSynthesisCall(
        string originalRequest,
        string combinedFindings,
        ILLMClient llmClient,
        string? model,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new() 
            { 
                Role = "system", 
                Content = "You are a synthesizer creating final coherent results from accumulated findings." 
            },
            new() 
            { 
                Role = "user", 
                Content = $@"Create a final well-structured response based on these accumulated findings:

{combinedFindings}

Original request: {originalRequest}

Provide a coherent, comprehensive final result that directly addresses the original request:" 
            }
        };
        
        return await llmClient.ChatAsync(messages, model, cancellationToken);
    }
}
