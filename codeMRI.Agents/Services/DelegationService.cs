using codeMRI.Agents.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

public class DelegationService
{
    private readonly ILogger<DelegationService> _logger;

    public DelegationService(ILogger<DelegationService> logger)
    {
        _logger = logger;
    }

    public bool ShouldDelegate(AgentTask task, object context)
    {
        // Simple heuristic: if payload is a large module or complex component
        if (task.Payload is CodeComponent component)
            if (component.ComplexityScore > 8 || component.LineCount > 500)
            {
                _logger.LogInformation(
                    "Delegation recommended for component {ComponentName} (Complexity: {Complexity})", component.Name,
                    component.ComplexityScore);
                return true;
            }

        return false;
    }

    public List<AgentTask> SplitTask(AgentTask originalTask)
    {
        var subTasks = new List<AgentTask>();

        if (originalTask.Payload is ModuleNode moduleNode)
            // Split by children
            foreach (var child in moduleNode.Children)
                subTasks.Add(new AgentTask
                {
                    Type = originalTask.Type,
                    Payload = child,
                    Metadata = new Dictionary<string, string>(originalTask.Metadata)
                    {
                        { "ParentId", originalTask.Id }
                    }
                });

        return subTasks;
    }
}