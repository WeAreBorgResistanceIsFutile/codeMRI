using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public abstract class BaseAgent : IAgent
{
    protected readonly ILogger _logger;
    protected readonly Services.AgentMessageBus _messageBus;

    public string Id { get; } = Guid.NewGuid().ToString();
    public abstract string Role { get; }

    protected BaseAgent(Services.AgentMessageBus messageBus, ILogger logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    public abstract Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken);
    
    public virtual bool CanHandle(AgentTask task)
    {
        return task.Type == Role;
    }

    public virtual Task<DelegationRequest?> ShouldDelegate(AgentTask task, CancellationToken cancellationToken)
    {
        return Task.FromResult<DelegationRequest?>(null);
    }

    protected async Task PublishStatusAsync(string status, string taskId)
    {
        await _messageBus.PublishAsync(new AgentMessage
        {
            SenderId = Id,
            MessageType = "AgentStatus",
            Content = new { Status = status, TaskId = taskId }
        });
    }
}
