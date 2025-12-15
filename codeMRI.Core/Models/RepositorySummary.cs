namespace codeMRI.Core.Models;

public class RepositorySummary
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsIngested { get; set; }
    public DateTime CreatedAt { get; set; }
}
