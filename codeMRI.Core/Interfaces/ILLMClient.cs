namespace codeMRI.Core.Interfaces;

/// <summary>
/// Interface for LLM clients that handle HTTP communication with language models.
/// This interface is for TRANSPORT ONLY - no message composition or truncation.
/// Use ILLMServiceFacade for high-level LLM interactions with automatic strategy selection.
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Send pre-composed messages to the LLM.
    /// Messages must already fit within context window - this method will throw if they don't.
    /// </summary>
    /// <param name="messages">Pre-composed messages that fit within context window.</param>
    /// <param name="model">Optional model override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if messages exceed context window.
    /// </exception>
    Task<string> ChatAsync(
        List<ChatMessage> messages,
        string? model = null,
        CancellationToken cancellationToken = default);
}


public class ChatMessage
{
    public ChatMessage() { }
    
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}