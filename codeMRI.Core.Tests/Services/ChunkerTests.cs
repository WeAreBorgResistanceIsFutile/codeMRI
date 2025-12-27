using NUnit.Framework;
using codeMRI.Core.Services;
using codeMRI.Core.Models;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class ChunkerTests
{
    [Test]
    public void CodeChunker_ShouldUse90PercentOfMaxTokens()
    {
        // Arrange
        // maxTokens = 100. Effective should be 90.
        // EstimateTokens is (int)(length / 4.0).
        // So 90 tokens is 360 characters.
        
        var chunker = new CodeChunker(maxTokens: 100);
        var node = new GraphNode { ComponentId = "test" };
        // Create content with 10 lines of 40 chars each = 400 chars total (100 tokens)
        // With 90% limit, should split into chunks
        var lines = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            lines.Add(new string('a', 40)); // 40 chars per line
        }
        node.Metadata.ContentSnippet = string.Join("\n", lines); // 400 chars + 9 newlines = 409 chars total
        
        // Act
        var chunks = chunker.ChunkCode(node);
        
        // Assert
        // 409 chars = 102 tokens, which is > 90, so should split
        Assert.That(chunks.Count, Is.GreaterThan(1));
    }

    [Test]
    public void CodeChunker_ShouldStillAllow1ChunkIfBelow90Percent()
    {
        // Arrange
        var chunker = new CodeChunker(maxTokens: 100);
        var node = new GraphNode { ComponentId = "test" };
        node.Metadata.ContentSnippet = new string('a', 360); // 90 tokens -> == 90
        
        // Act
        var chunks = chunker.ChunkCode(node);
        
        // Assert
        Assert.That(chunks.Count, Is.EqualTo(1));
    }
}
