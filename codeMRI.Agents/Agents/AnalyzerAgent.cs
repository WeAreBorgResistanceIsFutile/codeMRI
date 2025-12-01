using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class AnalyzerAgent : BaseAgent
{
    private readonly IComponentIdentificationService _componentService;

    public override string Role => "Analyzer";

    public AnalyzerAgent(
        AgentMessageBus messageBus,
        IComponentIdentificationService componentService,
        ILogger<AnalyzerAgent> logger,
        IASTServiceClient? astService = null) 
        : base(messageBus, logger, astService)
    {
        _componentService = componentService;
    }

    public override async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("AnalyzerAgent executing task {TaskId}", task.Id);

        try
        {
            if (task.Payload is string path)
            {
                var structure = await _componentService.AnalyzeRepositoryAsync(path);
                var components = await _componentService.IdentifyComponentsAsync(path);

                return new AgentResult
                {
                    TaskId = task.Id,
                    Success = true,
                    Output = new AnalysisResult { Structure = structure, Components = components }
                };
            }
            
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { "Invalid payload for Analyzer" } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzerAgent");
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { ex.Message } };
        }
    }
}
