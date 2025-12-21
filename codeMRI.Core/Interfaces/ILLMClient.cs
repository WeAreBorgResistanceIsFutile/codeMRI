namespace codeMRI.Core.Interfaces;

public interface ILLMClient
{
    int ContextSize { get; }

    Task<string> ChatAsync(string systemPrompt, string userPrompt, List<ChatMessage> history, string? model = null,
        CancellationToken cancellationToken = default);

    Task<string> ChatWithFindingsAsync(string systemPrompt, string basePrompt, string largeContent,
        string? model = null, CancellationToken cancellationToken = default, bool useSemanticChunking = true);
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