You are an expert software architect and developer practicing **Test-Driven Development (TDD)**. Your task is to implement the **Cross-Module Reference Management** system for intelligent cross-linking and dependency-aware resolution in `codeMRI`.

## Context
The current system has basic cross-reference extraction but lacks the sophisticated intelligent resolution system described in CodeWiki. We need to implement a global component registry and automated cross-linking.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Reference Resolution Testing**: Test cross-reference detection and resolution
3. **Registry Validation**: Test global component registry functionality
4. **Integration Testing**: Test reference management integration with documentation generation

## Objectives
1.  **Implement NUnit Tests First**:
    - Create `ReferenceManagementServiceTests.cs` with comprehensive test cases
    - Test cross-reference detection across module boundaries
    - Test global registry component tracking and lookup
    - Test intelligent resolution algorithms for different dependency types

2.  **Create `codeMRI.Core/Services/ReferenceManagementService.cs`**:
    - Implement `IReferenceManagement` interface
    - Create global component registry for tracking documented components
    - Implement intelligent cross-reference resolution system
    - Support automated hyperlink generation between related components

3.  **Implement Dependency-Aware Resolution**:
    - Analyze dependency types (inheritance, composition, usage)
    - Create appropriate cross-links based on relationship strength
    - Support navigation between related components in documentation
    - Avoid content duplication through intelligent reference management

4.  **Integrate with Documentation System**:
    - Update `WikiGenerationService` to use reference management
    - Automatically insert hyperlinks between related documentation sections
    - Ensure cross-references are maintained during documentation updates

## Constraints
- Maintain consistency across module boundaries
- Ensure intelligent resolution avoids content duplication
- Support all dependency types mentioned in CodeWiki
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.Core/Services/ReferenceManagementService.cs` (New)
- `codeMRI.Core/Services/WikiGenerationService.cs` (Update)
- `codeMRI.Core.Tests/Services/ReferenceManagementServiceTests.cs` (New)
- `codeMRI.Core.Tests/Services/WikiGenerationServiceTests.cs` (Update)

## Testing Strategy
- **Unit Tests**: Test individual reference resolution algorithms
- **Integration Tests**: Test full reference management workflow
- **Registry Tests**: Test global component registry functionality
- **Cross-Module Tests**: Test reference resolution across module boundaries
- **Edge Cases**: Test complex dependency scenarios and circular references

Generate the C# code for test files first, then the implementation files, following TDD principles. Include comprehensive test coverage for all reference management scenarios.
