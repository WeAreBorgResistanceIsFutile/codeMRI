using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Shared.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class ValidatorAgent : BaseAgent
{
    public override string Role => "Validator";

    public ValidatorAgent(AgentMessageBus messageBus, ILogger<ValidatorAgent> logger) 
        : base(messageBus, logger)
    {
    }

    public override Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        if (task.Payload is WikiStructure structure)
        {
            // Validate structure
            bool isValid = structure.Sections.Any();
            return Task.FromResult(new AgentResult
            {
                TaskId = task.Id,
                Success = isValid,
                Output = isValid ? "Valid" : "Empty Structure"
            });
        }
        
        return Task.FromResult(new AgentResult { TaskId = task.Id, Success = true });
    }
}
