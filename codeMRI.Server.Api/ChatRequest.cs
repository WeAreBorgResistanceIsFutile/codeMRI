namespace codeMRI.Server.Api;

public class ChatRequest
{
    public string Query { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = new();
    public string Language { get; set; } = "English";
}