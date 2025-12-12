using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class SynthesizerAgent : BaseAgent
{
    public SynthesizerAgent(AgentMessageBus messageBus, ILogger<SynthesizerAgent> logger,
        IASTServiceClient? astService = null)
        : base(messageBus, logger, astService)
    {
    }

    public override string Role => "Synthesizer";

    public override async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        await PublishTaskStartedAsync(task);
        await PublishStatusAsync("Synthesizing results", task.Id);

        try
        {
            AgentResult result;

            // Logic to combine wiki pages or structure
            if (task.Payload is List<WikiPage> pages)
            {
                var structure = new WikiStructure
                {
                    Title = "Generated Documentation",
                    Sections = new List<WikiSection>
                    {
                        new() { Title = "Components", PageRefs = pages.Select(p => p.Id).ToList() }
                    }
                };

                result = new AgentResult
                {
                    TaskId = task.Id,
                    Success = true,
                    Output = structure
                };
            }
            else
            {
                result = new AgentResult { TaskId = task.Id, Success = true };
            }

            // Publish synthesis complete
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = Id,
                MessageType = AgentMessageTypes.SynthesisComplete,
                Content = new { TaskId = task.Id }
            });

            await PublishTaskCompletedAsync(task, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SynthesizerAgent");
            await PublishTaskFailedAsync(task, ex);
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { ex.Message } };
        }
    }
}