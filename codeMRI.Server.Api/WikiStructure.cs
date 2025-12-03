namespace codeMRI.Server.Api;

public class WikiStructure
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WikiSection> Sections { get; set; } = new();
}