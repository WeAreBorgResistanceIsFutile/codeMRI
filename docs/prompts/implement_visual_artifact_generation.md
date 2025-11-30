# LLM Prompt: Implement Visual Artifact Generation

## Context
You are an AI assistant tasked with implementing visual artifact generation for the CodeWiki documentation system. The system currently produces text-only documentation and needs to include diagrams and visual representations.

## Implementation Plan
Based on the implementation plan in `docs/implementation_plans/03_visual_artifact_generation.md`, you need to:

1. Implement diagram generation
2. Create visual synthesis system
3. Integrate visual artifacts into documentation

## Task
Generate the necessary code to implement visual artifact generation. Follow these steps:

### Step 1: Create Visualization Project
- Create new project: `codeMRI.Visualization`
- Add Mermaid.js integration
- Add dependency on `codeMRI.Core`

### Step 2: Implement Diagram Generation
- Create architecture diagram generator
- Implement data flow visualization
- Add sequence diagram generation
- Create component diagram support

### Step 3: Implement Visual Synthesis
- Create diagram composition service
- Implement layout optimization
- Add responsive design system
- Implement theme support

### Step 4: Integration
- Update documentation templates to include diagrams
- Implement diagram caching
- Add versioning support
- Add accessibility features

## Constraints
- Use C# 10+ for backend
- Use TypeScript for frontend components
- Follow existing code style
- Add comprehensive documentation
- Include unit and integration tests

## Expected Output
- New visualization project
- Diagram generation services
- Visual synthesis system
- Integration with documentation pipeline
- Test coverage

## Example Structure
```csharp
// DiagramGeneratorService.cs
public class DiagramGeneratorService
{
    public async Task<string> GenerateArchitectureDiagram(ModuleTree moduleTree)
    {
        // Generate Mermaid.js code for architecture diagram
    }
    
    public async Task<string> GenerateDataFlow(CodeComponent component)
    {
        // Generate data flow diagram
    }
}
```

Now, please generate the complete implementation following these guidelines.
