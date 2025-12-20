namespace codeMRI.Server.Api;

public class IngestionRequest
{
    public string Url { get; set; } = string.Empty;
    public AudienceType Audience { get; set; } = AudienceType.Developer;
}