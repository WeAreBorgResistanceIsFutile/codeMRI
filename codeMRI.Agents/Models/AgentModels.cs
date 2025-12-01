namespace codeMRI.Agents.Models;

public record AgentTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Type { get; init; } = string.Empty;
    public object? Payload { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public record AgentResult
{
    public string TaskId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public object? Output { get; init; }
    public List<string> Errors { get; init; } = new();
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public record DelegationRequest
{
    public string TaskId { get; init; } = string.Empty;
    public string TargetAgentType { get; init; } = string.Empty;
    public AgentTask SubTask { get; init; } = new();
    public string Reason { get; init; } = string.Empty;
}

public record AgentMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string SenderId { get; init; } = string.Empty;
    public string TargetAgentId { get; init; } = string.Empty; // Empty for broadcast
    public string MessageType { get; init; } = string.Empty;
    public object? Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record CodeComplexityMetrics
{
    public int TokenCount { get; init; }
    public int CyclomaticComplexity { get; init; }
    public int NestingDepth { get; init; }
}
