# LLM Prompt: Implement Cross-Language Support

## Context
You are enhancing CodeWiki to support 7 programming languages with consistent analysis and documentation.

## Implementation Plan
Based on `docs/implementation_plans/04_cross_language_support.md`, implement:

1. Language parsers with Tree-sitter
2. Cross-language type system
3. Language services
4. Comprehensive testing

## Task
Generate code for:

### Step 1: Language Parsers
- Implement Tree-sitter parsers for 7 languages
- Create unified AST model
- Add parser caching

### Step 2: Type System
- Implement cross-language type resolution
- Add language bridge patterns
- Create type mapping

### Step 3: Language Services
- Add language-specific services
- Implement code navigation
- Add refactoring support

### Step 4: Testing
- Add language test suites
- Implement parser validation
- Create benchmarks

## Constraints
- C# 10+ backend
- Follow existing patterns
- Add documentation and tests

## Expected Output
- LanguageServices project
- Parser implementations
- Type system
- Test coverage

## Example
```csharp
public interface ILanguageService
{
    LanguageInfo GetLanguageInfo(string filePath);
    AstNode Parse(string code);
    TypeInfo ResolveType(string typeName);
}
