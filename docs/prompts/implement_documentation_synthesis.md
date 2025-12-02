You are an expert software architect and developer practicing **Test-Driven Development (TDD)**. Your task is to implement the **Advanced Documentation Synthesis** system for sophisticated LLM-based parent module synthesis in `codeMRI`.

## Context
The current parent module documentation generation is basic. We need to implement the complete hierarchical assembly approach described in CodeWiki Section 3.3, including multi-stage synthesis with theme analysis and pattern recognition.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Synthesis Validation**: Test LLM synthesis output quality and consistency
3. **Theme Analysis Testing**: Test pattern recognition and theme extraction algorithms
4. **Integration Testing**: Test synthesis integration with hierarchical documentation workflow

## Objectives
1.  **Implement NUnit Tests First**:
    - Create `DocumentationSynthesisServiceTests.cs` with comprehensive test cases
    - Test LLM synthesis prompts and response parsing
    - Test theme analysis and pattern recognition algorithms
    - Test hierarchical assembly workflow integration

2.  **Create `codeMRI.Core/Services/DocumentationSynthesisService.cs`**:
    - Implement `IDocumentationSynthesis` interface
    - Perform multi-stage synthesis as described in CodeWiki Section 3.3
    - Analyze child documentation for themes and patterns
    - Generate architectural overviews explaining module collaboration
    - Create feature summaries and usage guides

3.  **Implement Advanced Synthesis Algorithms**:
    - Theme analysis to identify recurring patterns across child modules
    - Architectural pattern recognition for system-level understanding
    - Usage guide generation based on public interfaces and APIs
    - Multi-level abstraction synthesis from implementation details to high-level concepts

4.  **Enhance Parent Module Processing**:
    - Implement sophisticated LLM prompting for parent module synthesis
    - Support recursive assembly from leaf components to system overview
    - Ensure consistency between child documentation and parent summaries
    - Generate comprehensive repository overview documentation

## Constraints
- Follow the exact synthesis approach described in CodeWiki Section 3.3
- Ensure synthesis maintains architectural coherence across abstraction levels
- Support multiple target audiences (developers, architects, users)
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.Core/Services/DocumentationSynthesisService.cs` (New)
- `codeMRI.Core/Services/WikiGenerationService.cs` (Update for synthesis integration)
- `codeMRI.Core.Tests/Services/DocumentationSynthesisServiceTests.cs` (New)
- `codeMRI.Core.Tests/Services/WikiGenerationServiceTests.cs` (Update)

## Testing Strategy
- **Unit Tests**: Test individual synthesis algorithms and theme analysis
- **Integration Tests**: Test full hierarchical assembly workflow
- **Quality Tests**: Test synthesis output quality and coherence
- **Pattern Recognition Tests**: Test architectural pattern detection accuracy
- **Edge Cases**: Test synthesis with incomplete or conflicting child documentation

Generate the C# code for test files first, then the implementation files, following TDD principles. Include comprehensive test coverage for all synthesis scenarios and integration points.
