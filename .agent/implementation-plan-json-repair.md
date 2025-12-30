# JSON Repair Service Implementation Plan

## Problem Statement

LLM responses often contain valid information but in malformed JSON format. Common issues include:

- JSON wrapped in markdown code blocks
- Chatty preambles/conclusions before/after JSON
- Trailing commas
- Missing closing braces/brackets
- Unescaped strings
- Comments in JSON

## Solution: Centralized JSON Extraction and Repair Service

### Design Principles

1. **Algorithmic** - No LLM dependency for repair
2. **Testable** - Following TDD with comprehensive test coverage
3. **Reusable** - Single service used across all LLM JSON parsing
4. **Progressive** - Multiple strategies from simple to complex
5. **Defensive** - Always has a fallback

### Architecture

```
IJsonRepairService
├── ExtractAndDeserialize<T>(string response)
└── ExtractJsonString(string response)

JsonRepairService (implementation)
├── Strategy 1: Clean extraction (markdown, whitespace)
├── Strategy 2: Brace matching with string tracking
├── Strategy 3: Syntax repair (trailing commas, quotes)
└── Strategy 4: Partial object reconstruction
```

### Implementation Steps (TDD)

#### Phase 1: Basic Extraction Tests & Implementation

1. **Test**: Extract JSON from markdown code block with ```json```
2. **Test**: Extract JSON from markdown code block with ````
3. **Test**: Extract JSON with text before/after
4. **Test**: Extract nested JSON objects
5. **Test**: Handle escaped strings correctly
6. **Test**: Handle strings with braces inside them

#### Phase 2: Repair Tests & Implementation

7. **Test**: Repair trailing comma in object
2. **Test**: Repair trailing comma in array
3. **Test**: Add missing closing brace
4. **Test**: Add missing closing bracket
5. **Test**: Handle unclosed strings (attempt)
6. **Test**: Remove JSON comments (// and /**/)

#### Phase 3: Integration Tests

13. **Test**: Real-world malformed responses from actual LLM outputs
2. **Test**: Edge cases (empty response, pure text, etc.)
3. **Test**: Performance test (large JSON strings)

#### Phase 4: Refactor Existing Services

16. Replace `DocumentationJudgeService.ExtractValidJson` with new service
2. Replace `NavigationStructureService.ExtractJson` with new service
3. Replace `RubricGenerationService.CleanJsonString` with new service
4. Update all tests to ensure no regressions

### API Design

```csharp
public interface IJsonRepairService
{
    /// <summary>
    /// Extracts and deserializes JSON from a potentially malformed LLM response
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="response">The LLM response containing JSON</param>
    /// <param name="options">Optional JsonSerializerOptions</param>
    /// <returns>Deserialized object or null if extraction/parsing fails</returns>
    T? ExtractAndDeserialize<T>(string response, JsonSerializerOptions? options = null)
        where T : class;
    
    /// <summary>
    /// Extracts valid JSON string from a potentially malformed response
    /// </summary>
    /// <param name="response">The response containing JSON</param>
    /// <returns>Extracted JSON string or null if extraction fails</returns>
    string? ExtractJsonString(string response);
    
    /// <summary>
    /// Attempts to repair common JSON syntax errors
    /// </summary>
    /// <param name="json">Potentially malformed JSON</param>
    /// <returns>Repaired JSON string</returns>
    string RepairJson(string json);
}
```

### Success Criteria

- [ ] All existing JSON parsing continues to work (no regressions)
- [ ] Test coverage > 90% for new service
- [ ] All services using JSON parsing refactored to use new service
- [ ] Performance: < 10ms for typical LLM responses
- [ ] Handles at least 15 different malformation patterns

### Benefits

1. **Consistency**: All JSON extraction uses same robust logic
2. **Maintainability**: One place to fix JSON parsing issues
3. **Testability**: Comprehensive test suite for JSON edge cases
4. **Reliability**: Fewer failed LLM response parsings
5. **Debuggability**: Centralized logging for JSON issues

### Future Enhancements

- JSON schema validation before deserialization
- Metrics/telemetry for common malformation patterns
- Auto-learning of new malformation patterns
- Fallback to local LLM only if algorithm fails (escape hatch)
