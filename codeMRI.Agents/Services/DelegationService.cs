using codeMRI.Agents.Configuration;
using codeMRI.Agents.Models;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Agents.Services;

public class DelegationService
{
    private readonly ILogger<DelegationService> _logger;
    private readonly AgentSettings _settings;
    public readonly AgentMessageBus MessageBus;

    public DelegationService(
        ILogger<DelegationService> logger,
        IOptions<AgentSettings> settings,
        AgentMessageBus messageBus)
    {
        _logger = logger;
        _settings = settings.Value;
        MessageBus = messageBus;
    }

    public bool ShouldDelegate(AgentTask task, object context)
    {
        // Check if delegation is enabled
        if (!_settings.EnableDelegation) return false;

        // Simple heuristic: if payload is a large module or complex component
        if (task.Payload is CodeComponent component)
        {
            var complexityThreshold = _settings.ComplexityThresholds.GetValueOrDefault("ComplexityScore", 8);
            var lineCountThreshold = _settings.ComplexityThresholds.GetValueOrDefault("LineCount", 500);

            if (component.ComplexityScore > complexityThreshold || component.LineCount > lineCountThreshold)
            {
                _logger.LogInformation(
                    "Delegation recommended for component {ComponentName} (Complexity: {Complexity}, Lines: {Lines})",
                    component.Name,
                    component.ComplexityScore,
                    component.LineCount);
                return true;
            }
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