using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

public interface ILLMClient
{
    Task<string> ChatAsync(string systemPrompt, string userPrompt, List<ChatMessage> history);
    IAsyncEnumerable<string> ChatStreamAsync(string systemPrompt, string userPrompt, List<ChatMessage> history);
}
