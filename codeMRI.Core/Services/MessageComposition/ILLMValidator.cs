using codeMRI.Core.Interfaces;

namespace codeMRI.Core.Services.MessageComposition;

/// <summary>
/// Validates messages against LLM constraints (context size, token limits, etc.).
/// Implemented by LLM clients to provide validation without HTTP coupling.
/// </summary>
public interface ILLMValidator
{
    /// <summary>
    /// Context window size in tokens.
    /// </summary>
    int ContextSize { get; }
    
    /// <summary>
    /// Recommended response buffer size in tokens.
    /// </summary>
    int ResponseBuffer { get; }
    
    /// <summary>
    /// Estimates token count for given text using LLM-specific tokenization.
    /// </summary>
    /// <param name="text">Text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokenCount(string text);
    
    /// <summary>
    /// Validates that messages fit within context window.
    /// </summary>
    /// <param name="messages">Messages to validate.</param>
    /// <returns>Validation result with details.</returns>
    MessageValidationResult ValidateMessages(List<ChatMessage> messages);
}

/// <summary>
/// Result of message validation.
/// </summary>
public class MessageValidationResult
{
    /// <summary>
    /// Whether the messages are valid (fit within context window).
    /// </summary>
    public required bool IsValid { get; init; }
    
    /// <summary>
    /// Estimated total tokens in the messages.
    /// </summary>
    public required int EstimatedTokens { get; init; }
    
    /// <summary>
    /// Available tokens for messages (ContextSize - ResponseBuffer).
    /// </summary>
    public required int AvailableTokens { get; init; }
    
    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
