namespace codeMRI.Core.Models;

public class WikiPage
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> RelevantFiles { get; set; } = new();
}