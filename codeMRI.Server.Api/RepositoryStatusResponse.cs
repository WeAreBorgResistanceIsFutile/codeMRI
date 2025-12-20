namespace codeMRI.Server.Api;

public class RepositoryStatusResponse
{
    public bool Exists { get; set; }
    public bool Ingested { get; set; }
    public string? Title { get; set; }
    public int PageCount { get; set; }
    public int SectionCount { get; set; }
}