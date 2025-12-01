You are an expert in AI evaluation frameworks. Your task is to implement the **CodeWikiBench Judge Agent** system.

## Context
The current evaluation system is heuristic-based. We need to implement the "Judge Agent" workflow described in the CodeWiki paper: using an LLM to evaluate specific rubric requirements for generated documentation.

## Objectives
1.  **Create `codeMRI.Core/Services/JudgeAgentService.cs`**:
    - Implement `IJudgeAgent`.
    - **Method: `EvaluateRequirementAsync(WikiPage page, RubricRequirement requirement)`**:
        - Construct a prompt: "Read this documentation: [Content]. Does it satisfy this requirement: [Requirement]? Answer Yes/No and explain."
        - Call `ILLMClient` with this prompt.
        - Parse the response to get a score (0.0 to 1.0) and reasoning.

2.  **Update `codeMRI.Core/Services/EvaluationMetricsSystem.cs`**:
    - Add a method `EvaluateWithJudgesAsync(WikiPage page, EvaluationRubric rubric)`.
    - Recursively traverse the rubric.
    - For leaf nodes, call `JudgeAgentService`.
    - For parent nodes, calculate the **Weighted Average** of children.
    - Return a `QualityScore` object with the final score and breakdown.

## Constraints
- Use `ILLMClient` for the Judge Agent.
- Strictly follow the weighted aggregation logic: `Score = Sum(w_i * s_i) / Sum(w_i)`.
- Keep the existing heuristic methods as "static checks" but prioritize the Judge score.

## Input Files
- `codeMRI.Core/Services/EvaluationMetricsSystem.cs`
- `codeMRI.Core/Services/JudgeAgentService.cs` (New)
- `codeMRI.Core/Services/RubricGenerationService.cs` (For Rubric models)

Generate the C# code for the new service and the updated evaluation system.