# LLM Prompt: Implement Multi-Agent Architecture

## Context
You are an AI assistant tasked with implementing a multi-agent architecture for the CodeWiki documentation generation system. The system currently has a monolithic pipeline that needs to be refactored into a dynamic, scalable multi-agent system.

## Implementation Plan
Based on the implementation plan in `docs/implementation_plans/01_multi_agent_architecture.md`, you need to:

1. Create the agent roles and interfaces
2. Implement agent communication
3. Add dynamic delegation
4. Set up agent lifecycle management

## Task
Generate the necessary code and configuration files to implement the multi-agent architecture. Follow these steps:

### Step 1: Create Project Structure
- Create new project: `codeMRI.Agents`
- Add references to `codeMRI.Core` and `codeMRI.Infrastructure`

### Step 2: Define Core Interfaces
Create the following interfaces in `codeMRI.Agents/Interfaces/`:
- `IAgent.cs`
- `IAgentCoordinator.cs`
- `IAgentFactory.cs`

### Step 3: Implement Agent Roles
Create the following agent implementations in `codeMRI.Agents/Agents/`:
- `AnalyzerAgent.cs`
- `DocumenterAgent.cs`
- `SynthesizerAgent.cs`
- `ValidatorAgent.cs`

### Step 4: Implement Communication
- Create `AgentMessageBus.cs` for inter-agent communication
- Implement message types in `codeMRI.Agents/Models/`
- Add serialization support

### Step 5: Implement Dynamic Delegation
- Create `DelegationService.cs`
- Implement complexity assessment
- Add delegation tracking

### Step 6: Update Documentation Pipeline
Modify `DocumentationGenerationPipeline.cs` to use the new agent system.

### Step 7: Add Configuration
Create configuration files for agent settings.

## Constraints
- Use C# 10+
- Follow existing code style
- Add XML documentation
- Include unit tests
- Ensure thread safety

## Expected Output
The complete implementation of the multi-agent system, including:
- Agent interfaces and implementations
- Message bus system
- Delegation logic
- Updated documentation pipeline
- Unit tests
- Configuration files

## Example Structure
```csharp
// IAgent.cs
public interface IAgent
{
    string Id { get; }
    Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken);
    bool CanHandle(AgentTask task);
    Task<DelegationRequest> ShouldDelegate(AgentTask task, CancellationToken cancellationToken);
}
```

Now, please generate the complete implementation following these guidelines.
