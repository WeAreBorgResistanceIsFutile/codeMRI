You are an expert software architect and developer practicing **Test-Driven Development (TDD)**. Your task is to implement the **Multi-Modal Visual Synthesis** system for generating architecture diagrams, data-flow representations, and sequence diagrams in `codeMRI`.

## Context
The current system lacks sophisticated visual artifact generation. We need to implement the complete multi-modal synthesis capability described in CodeWiki Section 3.3, including automatic diagram generation and integration with documentation.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Diagram Validation**: Test diagram generation logic and syntax validation
3. **Integration Tests**: Test diagram integration with documentation system
4. **Performance Testing**: Test diagram generation performance for large codebases

## Objectives
1.  **Implement NUnit Tests First**:
    - Create `DiagramGeneratorServiceTests.cs` with comprehensive test cases
    - Test Mermaid.js syntax generation for different diagram types
    - Test architecture pattern recognition algorithms
    - Test performance with different repository sizes

2.  **Create `codeMRI.Visualization/Services/DiagramGeneratorService.cs`**:
    - Implement `IDiagramGenerator` interface
    - Generate **Architecture Diagrams** from `ModuleTree` and `DependencyGraph`
    - Generate **Sequence Diagrams** from call graphs and execution paths
    - Generate **Data-Flow Diagrams** from variable usage and type propagation
    - Support automatic diagram type selection based on codebase characteristics

3.  **Update `codeMRI.Core/Services/WikiGenerationService.cs`**:
    - Integrate diagram generation into documentation workflow
    - Embed generated Mermaid markdown blocks into Wiki pages
    - Ensure diagrams are automatically regenerated on code changes

4.  **Implement Architecture Pattern Recognition**:
    - Identify architectural patterns (MVC, Microservices, Event-driven)
    - Generate appropriate diagrams for recognized patterns
    - Support cross-module visualization for system-wide interactions

## Constraints
- Use Mermaid.js syntax for all diagram generation
- Ensure diagrams are accurate representations of code structure
- Support all diagram types mentioned in CodeWiki: architecture, data-flow, sequence
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.Visualization/Services/DiagramGeneratorService.cs` (New)
- `codeMRI.Core/Services/WikiGenerationService.cs` (Update)
- `codeMRI.Visualization.Tests/Services/DiagramGeneratorServiceTests.cs` (New)
- `codeMRI.Core.Tests/Services/WikiGenerationServiceTests.cs` (Update)

## Testing Strategy
- **Unit Tests**: Test individual diagram generation algorithms
- **Integration Tests**: Test diagram integration with documentation system
- **Validation Tests**: Test Mermaid.js syntax correctness
- **Performance Tests**: Test diagram generation with large dependency graphs
- **Pattern Recognition Tests**: Test architecture pattern detection accuracy

Generate the C# code for test files first, then the implementation files, following TDD principles. Include comprehensive test coverage for all diagram types and integration scenarios.
