using codeMRI.Agents.Models;

namespace codeMRI.Agents.Interfaces;

public interface IAgent
{
    string Id { get; }
    string Role { get; }
    Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken);
    bool CanHandle(AgentTask task);
    Task<DelegationRequest?> ShouldDelegate(AgentTask task, CancellationToken cancellationToken);
}

public interface IAgentCoordinator
{
    Task<AgentResult> CoordinateTaskAsync(AgentTask task, CancellationToken cancellationToken);
    void RegisterAgent(IAgent agent);
    IAgent? GetAgentForTask(AgentTask task);
}
