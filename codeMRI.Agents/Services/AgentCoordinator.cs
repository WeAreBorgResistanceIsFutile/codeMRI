using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

public class AgentCoordinator : IAgentCoordinator
{
    private readonly List<IAgent> _agents = new();
    private readonly DelegationService _delegationService;
    private readonly ILogger<AgentCoordinator> _logger;

    public AgentCoordinator(
        DelegationService delegationService,
        ILogger<AgentCoordinator> logger,
        IEnumerable<IAgent> agents)
    {
        _delegationService = delegationService;
        _logger = logger;
        _agents.AddRange(agents);
    }

    public void RegisterAgent(IAgent agent)
    {
        _agents.Add(agent);
    }

    public IAgent? GetAgentForTask(AgentTask task)
    {
        return _agents.FirstOrDefault(a => a.CanHandle(task));
    }

    public async Task<AgentResult> CoordinateTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var agent = GetAgentForTask(task);
        if (agent == null)
            return new AgentResult
                { TaskId = task.Id, Success = false, Errors = { $"No agent found for role {task.Type}" } };

        // Check delegation
        DelegationRequest? delegationRequest = null;
        try
        {
            delegationRequest = await agent.ShouldDelegate(task, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking delegation for task {TaskId}", task.Id);
        }

        if (delegationRequest != null)
        {
            _logger.LogInformation("Task {TaskId} delegated to {Target}. Reason: {Reason}", task.Id,
                delegationRequest.TargetAgentType, delegationRequest.Reason);

            // Publish delegation event for telemetry and UI updates
            await _delegationService.MessageBus.PublishAsync(new AgentMessage
            {
                SenderId = agent.Id,
                MessageType = AgentMessageTypes.TaskDelegated,
                Content = new 
                { 
                    TaskId = task.Id,
                    FromAgent = agent.Role,
                    ToAgent = delegationRequest.TargetAgentType,
                    Reason = delegationRequest.Reason,
                    Timestamp = DateTime.UtcNow
                }
            });

            // Ensure SubTask has the correct target type
            var subTask = delegationRequest.SubTask with { Type = delegationRequest.TargetAgentType };

            // Recursive coordination
            return await CoordinateTaskAsync(subTask, cancellationToken);
        }

        // Execute
        return await agent.ExecuteAsync(task, cancellationToken);
    }
}