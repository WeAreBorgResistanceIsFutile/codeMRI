using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class AnalyzerAgent : BaseAgent
{
    private readonly IComponentIdentificationService _componentService;

    public AnalyzerAgent(
        AgentMessageBus messageBus,
        IComponentIdentificationService componentService,
        ILogger<AnalyzerAgent> logger,
        IASTServiceClient? astService = null)
        : base(messageBus, logger, astService)
    {
        _componentService = componentService;
    }

    public override string Role => "Analyzer";

    public override async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        await PublishTaskStartedAsync(task);
        _logger.LogInformation("AnalyzerAgent executing task {TaskId}", task.Id);

        try
        {
            if (task.Payload is string path)
            {
                await PublishStatusAsync("Analyzing repository structure", task.Id);
                var structure = await _componentService.AnalyzeRepositoryAsync(path);

                await PublishStatusAsync("Identifying components", task.Id);
                var components = await _componentService.IdentifyComponentsAsync(path);

                var result = new AgentResult
                {
                    TaskId = task.Id,
                    Success = true,
                    Output = new AnalysisResult { Structure = structure, Components = components }
                };

                // Publish analysis complete for downstream agents
                await _messageBus.PublishAsync(new AgentMessage
                {
                    SenderId = Id,
                    MessageType = AgentMessageTypes.AnalysisComplete,
                    Content = new { TaskId = task.Id, Structure = structure, Components = components }
                });

                await PublishTaskCompletedAsync(task, result);
                return result;
            }

            var failureResult = new AgentResult
                { TaskId = task.Id, Success = false, Errors = { "Invalid payload for Analyzer" } };
            await PublishTaskCompletedAsync(task, failureResult);
            return failureResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzerAgent");
            await PublishTaskFailedAsync(task, ex);
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { ex.Message } };
        }
    }
}