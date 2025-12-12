namespace codeMRI.Agents.Interfaces;

/// <summary>
/// Service for tracking and logging agent activities, metrics, and telemetry
/// </summary>
public interface IAgentTelemetryService
{
    /// <summary>
    /// Track a general agent activity
    /// </summary>
    void TrackAgentActivity(string agentId, string activity, Dictionary<string, object>? metadata = null);
    
    /// <summary>
    /// Track a delegation event between agents
    /// </summary>
    void TrackDelegation(string fromAgent, string toAgent, string reason, string taskId);
    
    /// <summary>
    /// Track task lifecycle events (started, completed, failed)
    /// </summary>
    void TrackTaskLifecycle(string taskId, string agentRole, string eventType, bool success, Dictionary<string, object>? metadata = null);
}
