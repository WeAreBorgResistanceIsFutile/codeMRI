# Fix for Embedding Context Length Error

## Problem

The application was encountering a 500 error from Ollama when trying to generate embeddings for documentation:

```
System.Net.Http.HttpRequestException: Response status code does not indicate success: 500 (Internal Server Error). 
Error: {"error":"the input length exceeds the context length"}
```

This error occurred in `OllamaEmbeddingService.GetEmbeddingAsync()` when the `DocumentationIndexer` attempted to embed wiki page content that exceeded the embedding model's maximum context length.

## Root Cause

While the `SemanticDocumentChunker` was configured to split content into chunks (MaxTokens: 2048), there was no safety mechanism to prevent individual chunks from exceeding the embedding model's absolute maximum context length (8192 tokens for `nomic-embed-text`).

The issue could occur when:

1. The chunker's token estimation was inaccurate
2. Specific content patterns caused the chunker to create oversized chunks
3. Edge cases in the chunking algorithm

## Solution

Implemented a **defense-in-depth** approach by adding input validation and truncation at the embedding service level:

### 1. Added Configuration

- **File**: `codeMRI.Infrastructure/Configuration/EmbeddingSettings.cs`
- **Change**: Added `MaxContextLength` property (default: 8192 tokens)
- **Purpose**: Define the absolute maximum context length supported by the embedding model

### 2. Implemented Input Validation

- **File**: `codeMRI.Infrastructure/Services/OllamaEmbeddingService.cs`
- **Changes**:
  - Added `ValidateAndTruncateInput()` method
  - Estimates token count using character count (1 token ≈ 4 characters)
  - Truncates oversized input to 90% of max context length (safety buffer)
  - Logs warnings when truncation occurs
  
### 3. Updated Configuration Files

Added `MaxContextLength: 8192` to:

- `codeMRI.Server/appsettings.json`
- `codeMRI.Server/appsettings.Development.json`
- `codeMRI.Benchmark/appsettings.json`

## Testing

Following TDD principles, we:

1. **Created failing tests** to reproduce the error:
   - `GetEmbeddingAsync_WhenInputExceedsContextLength_ShouldThrowHttpRequestException`
   - `IndexPageAsync_WhenContentExceedsContextLength_ShouldNotThrowException`

2. **Implemented the fix** with input validation and truncation

3. **Added verification tests**:
   - `GetEmbeddingAsync_WhenInputExceedsMaxContextLength_ShouldTruncateAndSucceed`
   - Verifies that oversized input is truncated to safe limits
   - Confirms the service succeeds instead of throwing an exception

4. **Verified all tests pass**: 55/55 tests in Infrastructure.Tests pass

## Impact

- **Prevents crashes**: The application will no longer crash when encountering oversized content
- **Graceful degradation**: Oversized content is truncated with a warning rather than failing completely
- **Maintains quality**: The chunker still operates normally; this is a safety net for edge cases
- **Observable**: Warnings are logged when truncation occurs, allowing monitoring and investigation

## Trade-offs

- **Information loss**: When truncation occurs, some content at the end of oversized chunks is lost
- **Acceptable**: This is preferable to complete failure, and the chunker should prevent this in normal operation
- **Monitoring**: Warnings allow us to identify and fix chunking issues if they occur frequently

## Next Steps

1. Monitor logs for truncation warnings
2. If truncation occurs frequently, investigate and improve the chunking algorithm
3. Consider making the safety margin (90%) configurable if needed
