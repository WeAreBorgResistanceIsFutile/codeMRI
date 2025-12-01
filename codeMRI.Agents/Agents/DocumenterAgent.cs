using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class DocumenterAgent : BaseAgent
{
    public override string Role => "Documenter";

    public DocumenterAgent(AgentMessageBus messageBus, ILogger<DocumenterAgent> logger, IASTServiceClient? astService = null) 
        : base(messageBus, logger, astService)
    {
    }

    public override Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DocumenterAgent executing task {TaskId}", task.Id);

        if (task.Payload is CodeComponent component)
        {
             var content = GenerateComponentContent(component);
             return Task.FromResult(new AgentResult
             {
                 TaskId = task.Id,
                 Success = true,
                 Output = new WikiPage
                 {
                     Id = component.Id,
                     Title = component.Name,
                     Content = content
                 }
             });
        }

        return Task.FromResult(new AgentResult { TaskId = task.Id, Success = false, Errors = { "Invalid payload" } });
    }

    private string GenerateComponentContent(CodeComponent component)
    {
        // Simplified logic - similar to original pipeline
        return $"# {component.Name}\n\nType: {component.Type}\n\n{component.Description}";
    }
}