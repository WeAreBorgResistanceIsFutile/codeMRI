You are an expert in AI evaluation frameworks practicing **Test-Driven Development (TDD)**. Your task is to implement the **CodeWikiBench Judge Agent** system.

## Context
The current evaluation system is heuristic-based. We need to implement the "Judge Agent" workflow described in the CodeWiki paper: using an LLM to evaluate specific rubric requirements for generated documentation.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Multi-Model Testing**: Test with different LLM backends
3. **Edge Cases**: Test binary decisions, reasoning parsing, aggregation logic
4. **Mock LLM Responses**: Simulate different judge behaviors

## Objectives
1.  **Implement NUnit Tests First**:
    - Create `JudgeAgentServiceTests.cs` with comprehensive test cases
    - Test binary scoring (0/1) validation for rubric requirements
    - Test reasoning extraction and parsing
    - Test hierarchical aggregation with weighted averages

2.  **Create `codeMRI.Core/Services/JudgeAgentService.cs`**:
    - Implement `IJudgeAgent`.
    - **Method: `EvaluateRequirementAsync(WikiPage page, RubricRequirement requirement)`**:
        - Construct a prompt: "Read this documentation: [Content]. Does it satisfy this requirement: [Requirement]? Answer Yes/No and explain."
        - Call `ILLMClient` with this prompt.
        - Parse the response to get a score (0.0 to 1.0) and reasoning.

3.  **Update `codeMRI.Core/Services/EvaluationMetricsSystem.cs`**:
    - Add a method `EvaluateWithJudgesAsync(WikiPage page, EvaluationRubric rubric)`.
    - Recursively traverse the rubric.
    - For leaf nodes, call `JudgeAgentService`.
    - For parent nodes, calculate the **Weighted Average** of children: `Score = Sum(w_i * s_i) / Sum(w_i)`.
    - Return a `QualityScore` object with the final score and breakdown.

4.  **Implement Reliability Tracking**:
    - Track standard deviation for multi-judge consensus
    - Implement uncertainty quantification as per CodeWiki Section 4.3
    - Test reliability calculations with synthetic data

## Constraints
- Use `ILLMClient` for the Judge Agent.
- Strictly follow the weighted aggregation logic from CodeWiki paper.
- Implement multi-model consensus evaluation (minimum 3 judge models).
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.Core/Services/EvaluationMetricsSystem.cs`
- `codeMRI.Core/Services/JudgeAgentService.cs` (New)
- `codeMRI.Core/Services/RubricGenerationService.cs` (For Rubric models)
- `codeMRI.Core.Tests/Services/JudgeAgentServiceTests.cs` (New - create comprehensive tests)
- `codeMRI.Core.Tests/Services/EvaluationMetricsSystemTests.cs` (Update existing tests)

## Testing Strategy
- **Unit Tests**: Test individual judge agent behaviors and scoring logic
- **Integration Tests**: Test hierarchical rubric traversal and aggregation
- **Mocking**: Use Moq for LLM client dependencies
- **Test Data**: Create realistic rubric scenarios and documentation samples
- **Edge Cases**: Test invalid responses, timeouts, and parsing errors

Generate the C# code for test files first, then the implementation files, following TDD principles. Include comprehensive test coverage for all edge cases and aggregation logic.
