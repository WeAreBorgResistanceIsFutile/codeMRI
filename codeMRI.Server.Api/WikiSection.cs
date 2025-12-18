namespace codeMRI.Server.Api;

public class WikiSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> PageRefs { get; set; } = new();
    public List<WikiSection> SubSections { get; set; } = new();
}