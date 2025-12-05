using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Infrastructure.Services;

public class TextSplitterService : IDocumentProcessor
{
    public IEnumerable<Document> Split(Document original, int chunkSize = 350, int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(original.Content)) yield break;

        const int MaxChunkCharSize = 1000; // Hard limit for Ollama / embedding models

        var words = original.Content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunkWords = new List<string>();
        var currentChunkLength = 0;
        var chunkIndex = 0;

        foreach (var word in words)
        {
            // Case 1: Word itself is huge (e.g., minified code or base64)
            if (word.Length > MaxChunkCharSize)
            {
                // Emit current buffer first
                if (currentChunkWords.Count > 0)
                {
                    yield return CreateChunk(original, string.Join(" ", currentChunkWords), chunkIndex++);
                    currentChunkWords.Clear();
                    currentChunkLength = 0;
                }

                // Split huge word into forced chunks
                for (var i = 0; i < word.Length; i += MaxChunkCharSize)
                {
                    var len = Math.Min(MaxChunkCharSize, word.Length - i);
                    yield return CreateChunk(original, word.Substring(i, len), chunkIndex++);
                }
                continue;
            }

            // Case 2: Adding word exceeds char limit
            // +1 for space
            if (currentChunkLength + word.Length + 1 > MaxChunkCharSize)
            {
                yield return CreateChunk(original, string.Join(" ", currentChunkWords), chunkIndex++);
                
                // Overlap logic implementation for char-limit based splitting is complex. 
                // For this hotfix, we clear and start new, possibly keeping last few words for context if we wanted to be fancy.
                // Keeping it simple: rigid split on size limit.
                currentChunkWords.Clear();
                currentChunkLength = 0;
            }

            currentChunkWords.Add(word);
            currentChunkLength += word.Length + 1;

            // Case 3: Token count limit (approximate with words count)
            if (currentChunkWords.Count >= chunkSize)
            {
                 yield return CreateChunk(original, string.Join(" ", currentChunkWords), chunkIndex++);
                 
                 // Handle overlap
                 var overlapCount = Math.Min(currentChunkWords.Count, overlap);
                 var overlapWords = currentChunkWords.Skip(currentChunkWords.Count - overlapCount).ToList();
                 currentChunkWords.Clear();
                 currentChunkWords.AddRange(overlapWords);
                 currentChunkLength = currentChunkWords.Sum(w => w.Length + 1);
            }
        }

        // Emit remaining
        if (currentChunkWords.Count > 0)
        {
            yield return CreateChunk(original, string.Join(" ", currentChunkWords), chunkIndex++);
        }
    }

    private Document CreateChunk(Document original, string content, int index)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = original.FilePath,
            Content = content,
            Metadata = new Dictionary<string, string>(original.Metadata)
        };
        doc.Metadata["chunk_index"] = index.ToString();
        doc.Metadata["parent_id"] = original.Id;
        return doc;
    }
}