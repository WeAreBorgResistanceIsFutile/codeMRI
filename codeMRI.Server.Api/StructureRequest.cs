namespace codeMRI.Server.Api;

public class StructureRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public string ReadmeContent { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public bool ForceRegenerate { get; set; } = false;
}