~~You are an expert software architect and developer practicing **Test-Driven Development (TDD)**. Your task is to implement **Cross-Language Support** for all 7 programming languages in `codeMRI`.

## Context
The current system has AST parsers for all 7 languages, but lacks unified cross-language processing and consistency. We need to implement the complete multilingual support described in CodeWiki, including unified dependency graph construction.

## TDD Approach
1. **Write Tests First**: Implement NUnit tests before production code
2. **Language-Specific Validation**: Test each language's parser and processing separately
3. **Cross-Language Integration**: Test unified processing across mixed-language repositories
4. **Performance Testing**: Test parsing and analysis performance for each language

## Objectives
1.  **Implement NUnit Tests First**:
    - Create language-specific test files for each of the 7 languages
    - Test AST parsing correctness for complex language constructs
    - Test dependency graph construction consistency across languages
    - Test cross-language reference resolution in mixed repositories

2.  **Enhance AST Service Language Support**:
    - Verify and enhance all 7 language parsers: Python, Java, JavaScript, TypeScript, C, C++, C#
    - Implement language-specific architectural pattern recognition
    - Enhance cross-language dependency analysis capabilities
    - Optimize parser performance for large codebases

3.  **Implement Unified Cross-Language Processing**:
    - Create unified dependency graph model using `depends_on` relation
    - Implement consistent entry point identification across languages
    - Support cross-language component registry and tracking
    - Ensure documentation quality consistency regardless of language

4.  **Enhance Language-Specific Features**:
    - Improve systems language support (C, C++) for complex constructs
    - Enhance managed language processing (C#, Java) for enterprise patterns
    - Optimize scripting language analysis (Python, JavaScript, TypeScript) for dynamic features

## Constraints
- Maintain consistency across all 7 supported languages
- Ensure unified processing model handles language differences transparently
- Support the original CodeWiki repository benchmark set
- **Test Requirements**: All code must be accompanied by comprehensive NUnit tests
- **TDD Mandatory**: Write tests before implementation code

## Input Files
- `codeMRI.ASTService/src/services/parserService.js` (Update)
- `codeMRI.Core/Services/HierarchicalDecompositionService.cs` (Language-specific updates)
- Language-specific test files for each of the 7 languages
- Cross-language integration test files

## Testing Strategy
- **Unit Tests**: Test each language's parser and processing separately
- **Integration Tests**: Test unified processing across multiple languages
- **Performance Tests**: Test parsing performance for each language
- **Consistency Tests**: Test documentation quality consistency across languages
- **Benchmark Tests**: Test against CodeWiki repository benchmark set

Generate the code for test files first, then the implementation files, following TDD principles. Include comprehensive test coverage for all language-specific scenarios and cross-language integration.~~
