using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Context containing all information needed for message composition strategies.
/// </summary>
public class MessageCompositionContext
{
    /// <summary>
    /// System prompt providing instructions to the LLM.
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Main text content to process (user prompt, code, documentation, etc.).
    /// Usually the longest part that may exceed context window.
    /// </summary>
    public string? TextToProcess { get; init; }
    
    /// <summary>
    /// Conversation history for chat-based interactions.
    /// </summary>
    public List<ChatMessage> History { get; init; } = new();
    
    /// <summary>
    /// Validator for checking message sizes and constraints.
    /// Provides context size, token estimation, etc.
    /// </summary>
    public required ILLMValidator Validator { get; init; }
    
    /// <summary>
    /// Optional model override.
    /// </summary>
    public string? Model { get; init; }
    
    /// <summary>
    /// Additional metadata for strategy-specific needs.
    /// Examples: UseSemanticChunking, UseRAG, UseMapReduce, etc.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

