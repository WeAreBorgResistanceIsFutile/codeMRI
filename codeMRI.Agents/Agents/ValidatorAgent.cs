using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Agents;

public class ValidatorAgent : BaseAgent
{
    public ValidatorAgent(AgentMessageBus messageBus, ILogger<ValidatorAgent> logger,
        IASTServiceClient? astService = null)
        : base(messageBus, logger, astService)
    {
        // Subscribe to documentation results
        messageBus.Subscribe(AgentMessageTypes.DocumentationComplete, async (message) =>
        {
            _logger.LogDebug("Received documentation complete notification: {Content}", message.Content);
        });
    }

    public override string Role => "Validator";

    public override async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        await PublishTaskStartedAsync(task);
        await PublishStatusAsync("Validating results", task.Id);

        try
        {
            AgentResult result;

            if (task.Payload is WikiStructure structure)
            {
                // Validate structure
                var isValid = structure.Sections.Any();
                result = new AgentResult
                {
                    TaskId = task.Id,
                    Success = isValid,
                    Output = isValid ? "Valid" : "Empty Structure"
                };
            }
            else
            {
                result = new AgentResult { TaskId = task.Id, Success = true };
            }

            // Publish validation complete
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = Id,
                MessageType = AgentMessageTypes.ValidationComplete,
                Content = new { TaskId = task.Id }
            });

            await PublishTaskCompletedAsync(task, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidatorAgent");
            await PublishTaskFailedAsync(task, ex);
            return new AgentResult { TaskId = task.Id, Success = false, Errors = { ex.Message } };
        }
    }
}