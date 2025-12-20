namespace codeMRI.Agents.Models;

/// <summary>
///     Standard message type constants for AgentMessageBus
/// </summary>
public static class AgentMessageTypes
{
    // Task lifecycle events
    public const string TaskStarted = "TaskStarted";
    public const string TaskCompleted = "TaskCompleted";
    public const string TaskFailed = "TaskFailed";

    // Delegation events
    public const string TaskDelegated = "TaskDelegated";

    // Status updates
    public const string AgentStatus = "AgentStatus";

    // Agent-specific completion events
    public const string AnalysisComplete = "AnalysisComplete";
    public const string DocumentationComplete = "DocumentationComplete";
    public const string ValidationComplete = "ValidationComplete";
    public const string SynthesisComplete = "SynthesisComplete";

    // Wildcard for subscribing to all message types
    public const string All = "*";
}