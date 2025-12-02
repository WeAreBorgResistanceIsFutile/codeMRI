You are an expert software architect and developer practicing **Test-Driven Development (TDD)**. Your task is to implement the **Complexity-Based Dynamic Delegation** logic for the Multi-Agent System in `codeMRI`.

## Context
The current `BaseAgent` and `AgentCoordinator` support basic delegation, but lack the specific logic described in the CodeWiki paper. We need agents to **analyze code complexity** and **automatically delegate** tasks when a module is too complex or large.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Red-Green-Refactor**: Follow the TDD cycle rigorously
3. **Test Coverage**: Aim for 80%+ coverage with meaningful assertions
4. **Mock Dependencies**: Use mocking frameworks for external services

## Objectives
1.  **Implement NUnit Tests First**:
    - Create `BaseAgentTests.cs` and `AgentCoordinatorTests.cs` with comprehensive test cases
    - Test edge cases: no delegation needed, single delegation, nested delegation
    - Test complexity thresholds and delegation criteria

2.  **Modify `codeMRI.Agents/Agents/BaseAgent.cs`**:
    - Implement `ShouldDelegate(AgentTask task)` to return a `DelegationRequest` if metrics exceed thresholds.
    - Add a method `CalculateComplexity(string code)` (or call a service) to get:
        - Token Count
        - Cyclomatic Complexity (approximate or via AST service)
        - Nesting Depth

3.  **Modify `codeMRI.Agents/Services/AgentCoordinator.cs`**:
    - Ensure it handles the recursive creation of sub-agents when a `DelegationRequest` is received.
    - Update the `ModuleTree` (conceptually) if delegation results in splitting a module (though for now, just delegating to a "Sub-Agent" for the same module or sub-parts is sufficient).

4.  **Configuration**:
    - Add constants or settings for `MaxTokens = 2000`, `MaxComplexity = 10`.

## Constraints
- Use the existing `IAgent`, `AgentTask`, and `DelegationRequest` classes.
- Keep the implementation generic enough to apply to `DocumenterAgent`.
- Mock the AST/Complexity calculation if the external service is not fully ready, but structure the code to call it.
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.Agents/Agents/BaseAgent.cs`
- `codeMRI.Agents/Services/AgentCoordinator.cs`
- `codeMRI.Agents/Models/AgentModels.cs`
- `codeMRI.Agents.Tests/Unit/BaseAgentTests.cs` (New - create comprehensive tests)
- `codeMRI.Agents.Tests/Unit/AgentCoordinatorTests.cs` (New - create comprehensive tests)

## Testing Strategy
- **Unit Tests**: Test individual agent behaviors and delegation logic
- **Integration Tests**: Test agent coordination and recursive delegation
- **Mocking**: Use Moq or equivalent for AST service dependencies
- **Test Data**: Create realistic complexity scenarios for delegation thresholds

Generate the C# code for both test files first, then the implementation files, following TDD principles.
