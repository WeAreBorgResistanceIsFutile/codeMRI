namespace codeMRI.Core.Interfaces;

public interface ILLMClient
{
    Task<string> ChatAsync(string systemPrompt, string userPrompt, List<ChatMessage> history);

    IAsyncEnumerable<string> ChatStreamAsync(string systemPrompt, string userPrompt, List<ChatMessage> history);
}

public class ChatMessage
{
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    public string Role { get; }
    public string Content { get; set; }
}