You are an expert in documentation engines and algorithms. Your task is to implement the **Hierarchical Bottom-Up Synthesis** pipeline.

## Context
Currently, documentation is generated in an unstructured way. We need to implement the "Phase 3: Hierarchical Assembly" from the CodeWiki paper, ensuring child modules are documented first, and their summaries are used to generate parent module documentation.

## Objectives
1.  **Update `codeMRI.Agents/Services/DocumentationGenerationPipeline.cs`**:
    - Implement a **Post-Order Traversal** method for the `ModuleTree`.
    - Logic:
        1. Visit children first.
        2. Generate docs for children.
        3. Collect summaries/descriptions of children.
        4. Visit current node (Parent).
        5. Generate Parent docs using Child Summaries + Dependency Graph.

2.  **Update `WikiGenerationService`**:
    - Add a parameter or method `GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages)`.
    - The prompt for this method should explicitly ask the LLM to "Synthesize an architectural overview based on these sub-modules...".

## Constraints
- Ensure the `ModuleTree` structure is respected.
- The pipeline must handle async operations (generating docs takes time).
- Use the existing `AgentCoordinator` to dispatch tasks if possible, but enforce the order.

## Input Files
- `codeMRI.Agents/Services/DocumentationGenerationPipeline.cs`
- `codeMRI.Core/Services/WikiGenerationService.cs`
- `codeMRI.Core/Models/ModuleTree.cs`

Generate the C# code to implement this recursive pipeline and the synthesis logic.