You are an expert software architect and developer practicing **Test-Driven Development (TDD)**. Your task is to implement the **Hierarchical Decomposition** system for breaking down large repositories into manageable modules in `codeMRI`.

## Context
The current decomposition system needs enhancement to support the sophisticated hierarchical decomposition described in CodeWiki Section 3.1, including dependency graph construction and entry point identification.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Dependency Testing**: Test graph construction and traversal algorithms
3. **Language-Specific Tests**: Test decomposition for all 7 supported languages
4. **Scalability Tests**: Test performance with large repository structures

## Objectives
1.  **Implement NUnit Tests First**:
    - Create `HierarchicalDecompositionServiceTests.cs` with comprehensive test cases
    - Test dependency graph construction for different language patterns
    - Test entry point identification algorithms
    - Test module tree generation and structure validation

2.  **Enhance `codeMRI.Core/Services/HierarchicalDecompositionService.cs`**:
    - Implement unified dependency graph construction with `depends_on` relations
    - Enhance entry point identification across all 7 languages
    - Implement recursive partitioning considering semantic coherence
    - Support feature-oriented module tree generation

3.  **Implement Language-Specific Decomposition**:
    - Create language-specific decomposition strategies for Python, Java, JavaScript, TypeScript, C, C++, C#
    - Handle language-specific entry points (main functions, API endpoints, CLI interfaces)
    - Support cross-language dependency analysis in mixed repositories

4.  **Enhance AST Service Integration**:
    - Improve cross-module reference extraction
    - Support advanced architectural pattern detection
    - Optimize performance for large-scale repository analysis

## Constraints
- Support all 7 programming languages consistently
- Ensure architectural coherence across decomposition boundaries
- Handle repositories of arbitrary size efficiently
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.Core/Services/HierarchicalDecompositionService.cs` (Update)
- `codeMRI.ASTService/src/services/analysisOrchestrator.js` (Update if needed)
- `codeMRI.Core.Tests/Services/HierarchicalDecompositionServiceTests.cs` (Update)
- Language-specific test files for each of the 7 languages

## Testing Strategy
- **Unit Tests**: Test individual decomposition algorithms
- **Integration Tests**: Test full repository decomposition workflow
- **Language Tests**: Test decomposition for each supported language
- **Performance Tests**: Test decomposition with large dependency graphs
- **Edge Cases**: Test repositories with complex nested structures

Generate the C# code for test files first, then the implementation files, following TDD principles. Include comprehensive test coverage for all language-specific decomposition scenarios.
