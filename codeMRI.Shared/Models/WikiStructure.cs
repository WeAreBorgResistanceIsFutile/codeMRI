namespace codeMRI.Shared.Models;

public class WikiStructure
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WikiSection> Sections { get; set; } = new();
}

public class WikiSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> PageRefs { get; set; } = new();
}