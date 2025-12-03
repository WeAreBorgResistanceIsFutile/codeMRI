namespace codeMRI.Server.Api;

public class IngestRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public bool Force { get; set; } = false;
    public bool Delete { get; set; } = false;
}