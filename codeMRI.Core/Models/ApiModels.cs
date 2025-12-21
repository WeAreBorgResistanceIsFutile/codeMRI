using System;
using System.Collections.Generic;

namespace codeMRI.Core.Models;

public class PageGenerationRequest
{
    public string Title { get; set; } = string.Empty;
    public string RepoPath { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public Dictionary<string, string> FileContents { get; set; } = new();
    public string Language { get; set; } = "English";
    public bool ForceRegenerate { get; set; }
}

public class IngestionRequest
{
    public string Url { get; set; } = string.Empty;
    public AudienceType Audience { get; set; }
}

public class StructureRequest
{
    public string RepoPath { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public string Language { get; set; } = "English";
    public bool SkipPersistence { get; set; }
    public bool ForceRegenerate { get; set; }
    public AudienceType Audience { get; set; } = AudienceType.Developer;
}

public class RepositoryStatusResponse
{
    public bool Exists { get; set; }
    public bool Ingested { get; set; }
    public string? Title { get; set; }
    public int PageCount { get; set; }
    public int SectionCount { get; set; }
}

public class ChatRequest
{
    public List<ChatMessageDto> History { get; set; } = new();
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class TaskLifecycleEvent
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Role { get; set; }
    public string? TaskType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AgentStatusEvent
{
    public string AgentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? TaskId { get; set; }
}

public class DelegationEvent
{
    public string FromAgentId { get; set; } = string.Empty;
    public string ToAgentId { get; set; } = string.Empty;
    public string FromAgent { get; set; } = string.Empty;
    public string ToAgent { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class StartIngestionResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class IngestionResult
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
