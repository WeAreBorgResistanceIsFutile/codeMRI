You are an expert software architect and developer. Your task is to implement the **Complexity-Based Dynamic Delegation** logic for the Multi-Agent System in `codeMRI`.

## Context
The current `BaseAgent` and `AgentCoordinator` support basic delegation, but lack the specific logic described in the CodeWiki paper. We need agents to **analyze code complexity** and **automatically delegate** tasks when a module is too complex or large.

## Objectives
1.  **Modify `codeMRI.Agents/Agents/BaseAgent.cs`**:
    - Implement `ShouldDelegate(AgentTask task)` to return a `DelegationRequest` if metrics exceed thresholds.
    - Add a method `CalculateComplexity(string code)` (or call a service) to get:
        - Token Count
        - Cyclomatic Complexity (approximate or via AST service)
        - Nesting Depth

2.  **Modify `codeMRI.Agents/Services/AgentCoordinator.cs`**:
    - Ensure it handles the recursive creation of sub-agents when a `DelegationRequest` is received.
    - Update the `ModuleTree` (conceptually) if delegation results in splitting a module (though for now, just delegating to a "Sub-Agent" for the same module or sub-parts is sufficient).

3.  **Configuration**:
    - Add constants or settings for `MaxTokens = 2000`, `MaxComplexity = 10`.

## Constraints
- Use the existing `IAgent`, `AgentTask`, and `DelegationRequest` classes.
- Keep the implementation generic enough to apply to `DocumenterAgent`.
- Mock the AST/Complexity calculation if the external service is not fully ready, but structure the code to call it.

## Input Files
- `codeMRI.Agents/Agents/BaseAgent.cs`
- `codeMRI.Agents/Services/AgentCoordinator.cs`
- `codeMRI.Agents/Models/AgentModels.cs`

Generate the C# code to update these files.