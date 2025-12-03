using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;

namespace codeMRI.Infrastructure.Services;

public class TextSplitterService : IDocumentProcessor
{
    public IEnumerable<Document> Split(Document original, int chunkSize = 350, int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(original.Content)) yield break;

        var words = original.Content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // Even if small, return as a "chunk" to ensure consistency (cloned, metadata added)
        if (words.Length <= chunkSize)
        {
            var chunkDoc = new Document
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = original.FilePath,
                Content = original.Content,
                Metadata = new Dictionary<string, string>(original.Metadata)
            };
            chunkDoc.Metadata["chunk_index"] = "0";
            chunkDoc.Metadata["parent_id"] = original.Id;
            yield return chunkDoc;
            yield break;
        }

        var step = chunkSize - overlap;
        if (step <= 0) step = 1;

        for (var i = 0; i < words.Length; i += step)
        {
            var length = Math.Min(chunkSize, words.Length - i);
            var chunkWords = new ArraySegment<string>(words, i, length);
            var chunkText = string.Join(" ", (IEnumerable<string>)chunkWords);

            var chunkDoc = new Document
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = original.FilePath,
                Content = chunkText,
                Metadata = new Dictionary<string, string>(original.Metadata)
            };

            chunkDoc.Metadata["chunk_index"] = (i / step).ToString();
            chunkDoc.Metadata["parent_id"] = original.Id;

            yield return chunkDoc;

            // If this chunk reached the end of the text, stop to avoid redundant smaller chunks
            if (i + length >= words.Length) break;
        }
    }
}