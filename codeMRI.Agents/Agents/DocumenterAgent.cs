using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class DocumenterAgent : BaseAgent, IDisposable
{
    private readonly Func<AgentMessage, Task> _analysisHandler;

    public DocumenterAgent(AgentMessageBus messageBus, ILogger<DocumenterAgent> logger,
        IASTServiceClient? astService = null)
        : base(messageBus, logger, astService)
    {
        // Subscribe to analysis results for potential use
        _analysisHandler = async (message) =>
        {
            _logger.LogDebug("Received analysis results: {Content}", message.Content);
        };
        messageBus.Subscribe(AgentMessageTypes.AnalysisComplete, _analysisHandler);
    }

    public void Dispose()
    {
        _messageBus.Unsubscribe(AgentMessageTypes.AnalysisComplete, _analysisHandler);
    }

    public override string Role => "Documenter";

    public override async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        await PublishTaskStartedAsync(task);
        _logger.LogInformation("DocumenterAgent executing task {TaskId}", task.Id);
        await PublishStatusAsync("Generating documentation", task.Id);

        try
        {
            AgentResult result;

            if (task.Payload is CodeComponent component)
            {
                var content = GenerateComponentContent(component);
                result = new AgentResult
                {
                    TaskId = task.Id,
                    Success = true,
                    Output = new WikiPage
                    {
                        Id = component.Id,
                        Title = component.Name,
                        Content = content
                    }
                };
            }
            else
            {
                result = new AgentResult { TaskId = task.Id, Success = false, Errors = { "Invalid payload" } };
            }

            // Publish documentation complete
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = Id,
                MessageType = AgentMessageTypes.DocumentationComplete,
                Content = new { TaskId = task.Id }
            });

            await PublishTaskCompletedAsync(task, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DocumenterAgent");
            await PublishTaskFailedAsync(task, ex);
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { ex.Message } };
        }
    }

    private string GenerateComponentContent(CodeComponent component)
    {
        // Simplified logic - similar to original pipeline
        return $"# {component.Name}\n\nType: {component.Type}\n\n{component.Description}";
    }
}