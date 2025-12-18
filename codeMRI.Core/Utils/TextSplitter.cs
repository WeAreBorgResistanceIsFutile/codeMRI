namespace codeMRI.Core.Utils;

public static class TextSplitter
{
    /// <summary>
    /// Splits a large text into chunks of specified size with overlap.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="chunkSize">The maximum size of each chunk (in characters).</param>
    /// <param name="overlap">The number of characters to overlap between chunks.</param>
    /// <returns>A list of text chunks.</returns>
    public static List<string> Split(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        if (chunkSize <= 0) throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));
        if (overlap < 0 || overlap >= chunkSize) throw new ArgumentException("Overlap must be non-negative and less than chunk size.", nameof(overlap));

        var chunks = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));

            if (start + length >= text.Length) break;

            start += chunkSize - overlap;
        }

        return chunks;
    }
}
