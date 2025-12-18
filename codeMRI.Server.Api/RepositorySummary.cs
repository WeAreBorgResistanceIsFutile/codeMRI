namespace codeMRI.Server.Api;

public class RepositorySummary
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RemoteUrl { get; set; }
    public bool IsIngested { get; set; }
    public DateTime CreatedAt { get; set; }
}
