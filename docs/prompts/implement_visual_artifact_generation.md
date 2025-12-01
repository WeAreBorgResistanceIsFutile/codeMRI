You are an expert software engineer specializing in data visualization and documentation. Your task is to implement the **Visual Artifact Generation** service for CodeWiki.

## Context
The system currently generates text-only markdown. We need to embed **Mermaid.js** diagrams to visualize the architecture, specifically **Class Diagrams** and **Sequence Diagrams**.

## Objectives
1.  **Create `codeMRI.Visualization/Services/DiagramGeneratorService.cs`**:
    - Implement `IDiagramGenerator`.
    - **Method 1: `GenerateClassDiagram(ModuleNode module, List<Dependency> dependencies)`**:
        - Iterate through the module's components.
        - Generate Mermaid `classDiagram` syntax showing inheritance (`<|--`), composition (`*--`), and associations (`-->`).
    - **Method 2: `GenerateSequenceDiagram(CodeComponent entryPoint, CallGraph graph)`**:
        - Trace the call graph from the entry point (up to a depth of 3-5).
        - Generate Mermaid `sequenceDiagram` syntax (`A->>B: Call()`).

2.  **Integrate into `WikiGenerationService`**:
    - Update `codeMRI.Core/Services/WikiGenerationService.cs` to inject `IDiagramGenerator`.
    - In `GeneratePageAsync`, call the generator for the current module.
    - Append the generated Mermaid markdown block to the page content.

## Constraints
- Use **Mermaid.js** syntax.
- Ensure the output is valid Markdown (wrapped in ````mermaid` blocks).
- Handle cases where dependency data is missing (fail gracefully or return empty string).

## Input Files
- `codeMRI.Visualization/Services/DiagramGeneratorService.cs` (New)
- `codeMRI.Core/Services/WikiGenerationService.cs`
- `codeMRI.Core/Models/ModuleTree.cs`

Generate the C# code for the new service and the update to the existing service.