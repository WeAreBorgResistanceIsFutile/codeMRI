using System.Text.RegularExpressions;

namespace codeMRI.Core.Services;

public class SemanticDocumentChunker
{
    private readonly int _maxTokens;
    private readonly int _overlapTokens;

    public SemanticDocumentChunker(int maxTokens = 512, int overlapTokens = 50)
    {
        _maxTokens = maxTokens;
        _overlapTokens = overlapTokens;
    }

    public List<string> ChunkMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return new List<string>();

        var chunks = new List<string>();
        // Split by headers first
        var sections = Regex.Split(markdown, @"(?=^#+\s)", RegexOptions.Multiline)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();

        string currentChunk = "";
        foreach (var section in sections)
        {
            if (EstimateTokens(currentChunk + section) <= _maxTokens)
            {
                currentChunk += section;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentChunk))
                {
                    chunks.Add(currentChunk);
                    // Carry over some overlap
                    currentChunk = GetOverlap(currentChunk) + section;
                }
                else
                {
                    // Section itself is too large, need to split it
                    var subChunks = SplitLargeText(section);
                    chunks.AddRange(subChunks.SkipLast(1));
                    currentChunk = subChunks.Last();
                }
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
            chunks.Add(currentChunk);

        return chunks;
    }

    private List<string> SplitLargeText(string text)
    {
        var result = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        string current = "";
        foreach (var p in paragraphs)
        {
            if (EstimateTokens(current + p) <= _maxTokens)
            {
                current += p + "\n\n";
            }
            else
            {
                if (!string.IsNullOrEmpty(current))
                    result.Add(current);
                current = p + "\n\n";
            }
        }
        
        if (!string.IsNullOrEmpty(current))
            result.Add(current);
            
        return result;
    }

    private string GetOverlap(string text)
    {
        // Simple overlap logic: take last few lines
        var lines = text.Split('\n');
        return string.Join('\n', lines.TakeLast(5));
    }

    private int EstimateTokens(string text)
    {
        return (int)(text.Length / 4.0);
    }
}
