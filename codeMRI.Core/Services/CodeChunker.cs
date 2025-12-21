using codeMRI.Core.Models;

namespace codeMRI.Core.Services;

public class CodeChunker
{
    private readonly int _maxTokens;

    public CodeChunker(int maxTokens = 512)
    {
        _maxTokens = maxTokens;
    }

    public List<CodeChunk> ChunkCode(GraphNode node)
    {
        var content = node.Metadata.ContentSnippet;
        if (string.IsNullOrWhiteSpace(content)) return new List<CodeChunk>();

        // For small components, return as single chunk
        if (EstimateTokens(content) <= _maxTokens)
        {
            return new List<CodeChunk>
            {
                new CodeChunk
                {
                    Text = content,
                    ComponentId = node.ComponentId,
                    FilePath = node.Metadata.FilePath,
                    Type = node.Metadata.Type
                }
            };
        }

        // For large components, split by lines for now 
        // (In a more advanced version, we'd use AST to split by methods/properties)
        var lines = content.Split('\n');
        var chunks = new List<CodeChunk>();
        var currentText = "";
        var startLine = 1;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i] + "\n";
            if (EstimateTokens(currentText + line) <= _maxTokens)
            {
                currentText += line;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentText))
                {
                    chunks.Add(new CodeChunk
                    {
                        Text = currentText,
                        ComponentId = node.ComponentId,
                        FilePath = node.Metadata.FilePath,
                        Type = node.Metadata.Type,
                        StartLine = startLine,
                        EndLine = startLine + i - 1
                    });
                }
                currentText = line;
                startLine = startLine + i;
            }
        }

        if (!string.IsNullOrEmpty(currentText))
        {
            chunks.Add(new CodeChunk
            {
                Text = currentText,
                ComponentId = node.ComponentId,
                FilePath = node.Metadata.FilePath,
                Type = node.Metadata.Type,
                StartLine = startLine,
                EndLine = startLine + lines.Length - 1
            });
        }

        return chunks;
    }

    private int EstimateTokens(string text)
    {
        return (int)(text.Length / 4.0);
    }
}

public class CodeChunk
{
    public string Text { get; set; } = string.Empty;
    public string ComponentId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
