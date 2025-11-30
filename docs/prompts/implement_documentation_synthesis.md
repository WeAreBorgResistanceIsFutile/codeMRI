# LLM Prompt: Implement Documentation Synthesis

## Context
Enhance documentation synthesis in CodeWiki for higher quality output.

## Implementation Plan
Based on `docs/implementation_plans/07_documentation_synthesis.md`:

1. Content synthesis
2. Structure optimization
3. Quality assurance
4. Personalization

## Task
Generate code for:

### Step 1: Content Synthesis
- Multi-stage LLM synthesis
- Architectural pattern docs
- Usage examples
- Best practices

### Step 2: Structure Optimization
- Hierarchical organization
- Navigation generation
- Responsive layouts
- Table of contents

### Step 3: Quality Assurance
- Documentation validation
- Style checking
- Quality metrics
- Linting rules

### Step 4: Personalization
- User preferences
- Customization
- Template system
- Themes

## Constraints
- C# 10+ backend
- Follow existing patterns
- Add documentation and tests

## Expected Output
- Enhanced DocumentationGenerationPipeline
- DocumentationSynthesizer service
- Quality control
- Test coverage

## Example
```csharp
public class DocumentationSynthesizer
{
    public async Task<DocumentationSet> Synthesize(ModuleTree moduleTree, SynthesisOptions options)
    {
        // Implementation
    }
}
