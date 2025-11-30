# LLM Prompt: Implement Hierarchical Decomposition Enhancement

## Context
You are an AI assistant tasked with enhancing the hierarchical decomposition service in the CodeWiki documentation system. The current implementation needs improvements in semantic clustering and architectural pattern recognition.

## Implementation Plan
Based on the implementation plan in `docs/implementation_plans/02_hierarchical_decomposition.md`, you need to:

1. Implement semantic clustering
2. Enhance dependency analysis
3. Optimize module tree structure
4. Add quality metrics

## Task
Generate the necessary code to enhance the hierarchical decomposition service. Follow these steps:

### Step 1: Update HierarchicalDecompositionService
- Implement Louvain community detection
- Add architectural pattern recognition
- Create language-specific clustering rules

### Step 2: Enhance Dependency Analysis
- Add cross-language dependency tracking
- Implement cyclic dependency detection
- Add architectural dependency types

### Step 3: Optimize Module Tree
- Implement token-based size constraints
- Add balanced tree algorithms
- Create architectural layer identification

### Step 4: Add Quality Metrics
- Implement cohesion metrics
- Add coupling analysis
- Create module quality scoring

## Constraints
- Use C# 10+
- Follow existing code style
- Add XML documentation
- Include unit tests
- Optimize for performance

## Expected Output
- Enhanced HierarchicalDecompositionService
- New ArchitecturalPatternService
- Updated dependency graph model
- Quality metrics system
- Comprehensive unit tests

## Example Structure
```csharp
// ArchitecturalPatternService.cs
public class ArchitecturalPatternService
{
    public ArchitecturalPattern RecognizePattern(ModuleNode module, DependencyGraph graph)
    {
        // Implementation here
    }
    
    private bool IsLayeredArchitecture(ModuleNode module) { ... }
    private bool IsMicroservices(ModuleNode module) { ... }
    private bool IsEventDriven(ModuleNode module) { ... }
}
```

Now, please generate the complete implementation following these guidelines.
