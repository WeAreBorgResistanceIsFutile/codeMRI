namespace codeMRI.Server.Api;

public enum AudienceType
{
    Developer,
    User,
    DevOps,
    All
}

public class StructureRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public string ReadmeContent { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public bool ForceRegenerate { get; set; } = false;
    public bool SkipPersistence { get; set; } = false;
    public string? ConnectionId { get; set; }
    public AudienceType Audience { get; set; } = AudienceType.Developer;
}