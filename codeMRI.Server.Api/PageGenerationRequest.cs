namespace codeMRI.Server.Api;

public class PageGenerationRequest
{
    public string Title { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public string RepoPath { get; set; } = string.Empty;
    public Dictionary<string, string> FileContents { get; set; } = new();
    public string Language { get; set; } = "English";
    public bool ForceRegenerate { get; set; } = false;
}