using codeMRI.Agents.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace codeMRI.Agents.Services;

public class AgentFactory : IAgentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AgentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IAgent CreateAgent(string agentType)
    {
        var agents = _serviceProvider.GetServices<IAgent>();
        var agent = agents.FirstOrDefault(a => a.Role == agentType);
        if (agent == null) throw new ArgumentException($"Agent type {agentType} not found");
        return agent;
    }
}