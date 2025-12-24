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
        var accumulatedFindings = string.Empty;
        
        for (var i = 0; i < chunks.Count; i++)
        {
            // Ensure findings don't exceed 30% of available input
            var maxFindingsTokens = (int)(availableInputTokens * 0.3);
            var findingsTokens = context.Validator.EstimateTokenCount(accumulatedFindings);
            
            if (findingsTokens > maxFindingsTokens)
            {
                _logger.LogWarning(
                    "Accumulated findings too large ({FindingsTokens} tokens). Truncating to {MaxTokens} tokens",
                    findingsTokens,
                    maxFindingsTokens);
                
                var maxFindingsChars = maxFindingsTokens * 4;
                accumulatedFindings = "... [EARLIER FINDINGS TRUNCATED]\n\n" + 
                                    accumulatedFindings.Substring(Math.Max(0, accumulatedFindings.Length - maxFindingsChars));
            }
            
            // Build chunk prompt
            var chunkPrompt = BuildChunkPrompt(
                context.SystemPrompt ?? "",
                chunks[i],
                i + 1,
                chunks.Count,
                accumulatedFindings
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
            
            accumulatedFindings = await llmClient.ChatAsync(messages, context.Model, cancellationToken);
        }
        
        // RESULT RECONSTRUCTION: Synthesize final coherent result
        _logger.LogInformation("Reconstructing final result from accumulated findings ({FindingsLength} chars)", 
            accumulatedFindings.Length);
        
        var finalResult = await SynthesizeFinalResult(
            context.SystemPrompt ?? "",
            accumulatedFindings,
            llmClient,
            context.Model,
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
                ["AccumulatedFindingsLength"] = accumulatedFindings.Length
            }
        };
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
        string accumulatedFindings,
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

{accumulatedFindings}

Original request: {originalRequest}

Provide a coherent, comprehensive final result that directly addresses the original request:" 
            }
        };
        
        return await llmClient.ChatAsync(messages, model, cancellationToken);
    }
}
