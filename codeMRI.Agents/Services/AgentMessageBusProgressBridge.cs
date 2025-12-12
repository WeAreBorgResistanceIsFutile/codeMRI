using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

/// <summary>
/// Bridges AgentMessageBus to IProgressService for unified progress reporting
/// </summary>
public class AgentMessageBusProgressBridge
{
    private readonly AgentMessageBus _messageBus;
    private readonly ILogger<AgentMessageBusProgressBridge> _logger;
    private IProgressService? _progressService;

    public AgentMessageBusProgressBridge(
        AgentMessageBus messageBus,
        ILogger<AgentMessageBusProgressBridge> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
        
        // Subscribe to agent status messages
        _messageBus.Subscribe(AgentMessageTypes.AgentStatus, async (message) =>
        {
            await HandleAgentStatusAsync(message);
        });
    }

    /// <summary>
    /// Set the progress service to forward messages to
    /// </summary>
    public void SetProgressService(IProgressService progressService)
    {
        _progressService = progressService;
    }

    private Task HandleAgentStatusAsync(AgentMessage message)
    {
        if (_progressService == null)
            return Task.CompletedTask;

        try
        {
            if (message.Content is { } content)
            {
                var contentDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    System.Text.Json.JsonSerializer.Serialize(content));

                if (contentDict != null)
                {
                    var status = contentDict.TryGetValue("Status", out var statusObj) ? statusObj.ToString() : "Processing";
                    var role = contentDict.TryGetValue("Role", out var roleObj) ? roleObj.ToString() : "Agent";

                    _progressService.Report(new ProgressInfo
                    {
                        Phase = role ?? "Agent",
                        Message = status ?? "Processing"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error forwarding agent status to progress service");
        }

        return Task.CompletedTask;
    }
}
