namespace codeMRI.Shared.DTOs;

public class IngestRequest
{
    public string RepoPath { get; set; } = string.Empty;
}

public class StructureRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public string ReadmeContent { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
}

public class PageGenerationRequest
{
    public string Title { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public string RepoPath { get; set; } = string.Empty;
    public Dictionary<string, string> FileContents { get; set; } = new();
    public string Language { get; set; } = "English";
}

public class ChatRequest
{
    public string Query { get; set; } = string.Empty;
    public List<Models.ChatMessage> History { get; set; } = new();
    public string Language { get; set; } = "English";
}
