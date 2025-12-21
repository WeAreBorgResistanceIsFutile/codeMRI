using System.Text;
using System.Text.RegularExpressions;

namespace codeMRI.Core.Utils;

public static class TextSplitter
{
    private static readonly Regex HeaderRegex = new(@"^#+\s+", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    ///     Splits a large text into chunks of specified size with overlap.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="chunkSize">The maximum size of each chunk (in characters).</param>
    /// <param name="overlap">The number of characters to overlap between chunks.</param>
    /// <returns>A list of text chunks.</returns>
    public static List<string> Split(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        if (chunkSize <= 0) throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));
        if (overlap < 0 || overlap >= chunkSize)
            throw new ArgumentException("Overlap must be non-negative and less than chunk size.", nameof(overlap));

        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));

            if (start + length >= text.Length) break;

            start += chunkSize - overlap;
        }

        return chunks;
    }

    /// <summary>
    ///     Splits text semantically by markdown headers, trying to keep related content together.
    ///     Falls back to simple splitting if no headers are found or if sections are too large.
    /// </summary>
    public static List<string> SplitSemantically(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();

        var matches = HeaderRegex.Matches(text);
        if (matches.Count == 0) return Split(text, chunkSize, overlap);

        var chunks = new List<string>();

        // Simple implementation: split at headers, but merge small sections
        var currentChunk = new StringBuilder();

        var positions = new List<int>();
        foreach (Match match in matches) positions.Add(match.Index);
        positions.Add(text.Length);

        for (var i = 0; i < positions.Count - 1; i++)
        {
            var start = positions[i];
            var end = positions[i + 1];
            var section = text.Substring(start, end - start);

            if (section.Length > chunkSize)
            {
                // If we have a pending chunk, flash it
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }

                // Split the oversized section
                chunks.AddRange(Split(section, chunkSize, overlap));
            }
            else if (currentChunk.Length + section.Length > chunkSize)
            {
                // Current chunk full, flash it
                chunks.Add(currentChunk.ToString());
                currentChunk.Clear();
                currentChunk.Append(section);
            }
            else
            {
                // Add to current chunk
                currentChunk.Append(section);
            }
        }

        if (currentChunk.Length > 0) chunks.Add(currentChunk.ToString());

        return chunks;
    }
}