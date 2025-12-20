namespace codeMRI.CLI;

public class WikiStructure
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WikiPage> Pages { get; set; } = new();
}

public class WikiPage
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ProgressInfo
{
    public string Phase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Percentage { get; set; }
}