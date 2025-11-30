using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

public class AgentCoordinator : IAgentCoordinator
{
    private readonly List<IAgent> _agents = new();
    private readonly ILogger<AgentCoordinator> _logger;
    private readonly DelegationService _delegationService;

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
        {
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { $"No agent found for role {task.Type}" } };
        }

        // Check delegation
        var delegationRequest = await agent.ShouldDelegate(task, cancellationToken);
        if (delegationRequest != null)
        {
            _logger.LogInformation("Task {TaskId} delegated to {Target}", task.Id, delegationRequest.TargetAgentType);
            // Recursive coordination or dispatch to specific agent
            // For simplicity, we just execute the subtask
            return await CoordinateTaskAsync(delegationRequest.SubTask, cancellationToken);
        }

        // Execute
        return await agent.ExecuteAsync(task, cancellationToken);
    }
}
