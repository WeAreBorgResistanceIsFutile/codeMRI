namespace codeMRI.Server.Api;

public class AgentMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public object? Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AgentStatusEvent
{
    public string Status { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class DelegationEvent
{
    public string TaskId { get; set; } = string.Empty;
    public string FromAgent { get; set; } = string.Empty;
    public string ToAgent { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class TaskLifecycleEvent
{
    public string TaskId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty; // For Started
    public bool Success { get; set; } // For Completed
    public string Error { get; set; } = string.Empty; // For Failed
    public DateTime Timestamp { get; set; }
}