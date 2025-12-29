using Microsoft.SemanticKernel.Text;

namespace codeMRI.Core.Services;

/// <summary>
/// Wrapper around Microsoft.SemanticKernel.Text.TextChunker for consistent API
/// </summary>
public class SemanticDocumentChunker
{
    private readonly int _maxTokens;
    private readonly int _overlapTokens;

    public SemanticDocumentChunker(int maxTokens = 512, int overlapTokens = 50)
    {
        // Use 90% of the set token count for the context window safety margin
        _maxTokens = (int)(maxTokens * 0.9);
        _overlapTokens = overlapTokens;
    }

    public List<string> ChunkMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return new List<string>();

        // Use Semantic Kernel's TextChunker which properly handles all text types
        // TextChunker's maxTokensPerLine expects token counts and uses internal estimation
        var maxChunkSize = _maxTokens;
        var overlapSize = _overlapTokens;

#pragma warning disable SKEXP0050 // TextChunker is experimental but stable enough for our use
        var lines = TextChunker.SplitPlainTextLines(markdown, maxChunkSize);
        var chunks = TextChunker.SplitPlainTextParagraphs(lines, maxChunkSize, overlapSize);
#pragma warning restore SKEXP0050

        return chunks;
    }
}
