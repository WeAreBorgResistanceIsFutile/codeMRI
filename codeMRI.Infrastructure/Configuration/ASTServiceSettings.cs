namespace codeMRI.Infrastructure.Configuration;

public class ASTServiceSettings
{
    public const string SectionName = "ASTService";

    public string BaseUrl { get; set; } = "http://localhost:3000";
    public int TimeoutSeconds { get; set; } = 120;
    public bool Enabled { get; set; } = true;
}