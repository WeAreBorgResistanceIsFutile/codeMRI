using codeMRI.Shared.Models;

namespace codeMRI.Shared.DTOs;

public class IngestRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public bool Force { get; set; } = false;
    public bool Delete { get; set; } = false;
}

public class StructureRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public string ReadmeContent { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public bool ForceRegenerate { get; set; } = false;
}

public class PageGenerationRequest
{
    public string Title { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public string RepoPath { get; set; } = string.Empty;
    public Dictionary<string, string> FileContents { get; set; } = new();
    public string Language { get; set; } = "English";
    public bool ForceRegenerate { get; set; } = false;
}

public class ChatRequest
{
    public string Query { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = new();
    public string Language { get; set; } = "English";
}