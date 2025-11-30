# LLM Prompt: Implement Evaluation System (CodeWikiBench)

## Context
Implement CodeWikiBench evaluation framework for documentation quality assessment.

## Implementation Plan
Based on `docs/implementation_plans/06_evaluation_system.md`:

1. Rubric system
2. Evaluation agents
3. Reporting dashboard
4. Integration hooks

## Task
Generate code for:

### Step 1: Rubric System
- Hierarchical rubric model
- Rubric generation
- Validation

### Step 2: Evaluation Agents
- Judge agents
- Scoring system
- Reliability metrics

### Step 3: Reporting
- Dashboard
- Metric visualization
- Export

### Step 4: Integration
- Evaluation hooks
- Continuous evaluation
- Quality gates

## Constraints
- C# 10+ backend
- Follow existing patterns
- Add documentation and tests

## Expected Output
- CodeWikiBench project
- Evaluation system
- Reporting features
- Test coverage

## Example
```csharp
public class EvaluationOrchestrator
{
    public async Task<EvaluationResult> Evaluate(DocumentationSet docs, Rubric rubric)
    {
        // Implementation
    }
}
