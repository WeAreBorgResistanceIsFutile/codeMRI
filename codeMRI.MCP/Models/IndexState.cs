namespace codeMRI.MCP.Models;

public enum IndexState
{
    Initializing,
    Indexing,
    Ready,
    Stale
}

public class QueryGateResult
{
    public bool Allowed { get; set; }
    public string? BlockReason { get; set; }
    public int EstimatedWaitSeconds { get; set; }

    public static QueryGateResult Allow() => new() { Allowed = true };
    
    public static QueryGateResult Block(string reason, int estimatedWaitSeconds = 5) => new()
    {
        Allowed = false,
        BlockReason = reason,
        EstimatedWaitSeconds = estimatedWaitSeconds
    };
}

public class FileChangeNotification
{
    public List<string> Files { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
