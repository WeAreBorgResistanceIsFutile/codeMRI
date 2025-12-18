using codeMRI.Core.Interfaces;
using codeMRI.Agents.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

/// <summary>
/// Implementation of telemetry service that logs structured agent activity data
/// </summary>
public class AgentTelemetryService : IAgentTelemetryService
{
    private readonly ILogger<AgentTelemetryService> _logger;
    private readonly AgentMessageBus _messageBus;

    public AgentTelemetryService(
        ILogger<AgentTelemetryService> logger,
        AgentMessageBus messageBus)
    {
        _logger = logger;
        _messageBus = messageBus;
        
        // Subscribe to all message types for comprehensive telemetry
        InitializeSubscriptions();
    }

    private void InitializeSubscriptions()
    {
        // Subscribe to all message types using wildcard
        _messageBus.Subscribe(AgentMessageTypes.All, async (message) =>
        {
            await LogMessageAsync(message);
        });
    }

    public void TrackAgentActivity(string agentId, string activity, Dictionary<string, object>? metadata = null)
    {
        _logger.LogInformation(
            "Agent Activity: {AgentId} - {Activity} {@Metadata}",
            agentId,
            activity,
            metadata ?? new Dictionary<string, object>());
    }

    public void TrackDelegation(string fromAgent, string toAgent, string reason, string taskId)
    {
        _logger.LogInformation(
            "Task Delegation: {TaskId} - From {FromAgent} to {ToAgent}. Reason: {Reason}",
            taskId,
            fromAgent,
            toAgent,
            reason);
    }

    public void TrackTaskLifecycle(string taskId, string agentRole, string eventType, bool success, Dictionary<string, object>? metadata = null)
    {
        var logLevel = success ? LogLevel.Information : LogLevel.Warning;
        
        _logger.Log(
            logLevel,
            "Task Lifecycle: {TaskId} - {AgentRole} - {EventType} - Success: {Success} {@Metadata}",
            taskId,
            agentRole,
            eventType,
            success,
            metadata ?? new Dictionary<string, object>());
    }

    private async Task LogMessageAsync(AgentMessage message)
    {
        _logger.LogDebug(
            "Message Bus Event: {MessageType} from {SenderId} at {Timestamp}",
            message.MessageType,
            message.SenderId,
            message.Timestamp);

        try
        {
            switch (message.MessageType)
            {
                case AgentMessageTypes.AgentStatus:
                    if (message.Content is { } statusContent)
                    {
                        var content = statusContent.ToString() ?? "";
                        TrackAgentActivity(message.SenderId, content);
                    }
                    break;

                case AgentMessageTypes.TaskDelegated:
                    // Using dynamic or manual parsing for simplicity in this implementation
                    var delegatedJson = System.Text.Json.JsonSerializer.Serialize(message.Content);
                    var delegatedData = System.Text.Json.JsonSerializer.Deserialize<DelegationTelemetryData>(delegatedJson);
                    if (delegatedData != null)
                    {
                        TrackDelegation(delegatedData.FromAgent, delegatedData.ToAgent, delegatedData.Reason, delegatedData.TaskId);
                    }
                    break;

                case AgentMessageTypes.TaskStarted:
                case AgentMessageTypes.TaskCompleted:
                case AgentMessageTypes.TaskFailed:
                    var lifecycleJson = System.Text.Json.JsonSerializer.Serialize(message.Content);
                    var lifecycleData = System.Text.Json.JsonSerializer.Deserialize<LifecycleTelemetryData>(lifecycleJson);
                    if (lifecycleData != null)
                    {
                        bool success = message.MessageType != AgentMessageTypes.TaskFailed;
                        TrackTaskLifecycle(lifecycleData.TaskId, lifecycleData.Role, message.MessageType, success);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing telemetry message of type {MessageType}", message.MessageType);
        }

        await Task.CompletedTask;
    }

    private class DelegationTelemetryData
    {
        public string TaskId { get; set; } = "";
        public string FromAgent { get; set; } = "";
        public string ToAgent { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    private class LifecycleTelemetryData
    {
        public string TaskId { get; set; } = "";
        public string Role { get; set; } = "";
    }
}
