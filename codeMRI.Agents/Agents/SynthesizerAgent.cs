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

    public override Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
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

            return Task.FromResult(new AgentResult
            {
                TaskId = task.Id,
                Success = true,
                Output = structure
            });
        }

        return Task.FromResult(new AgentResult { TaskId = task.Id, Success = true });
    }
}