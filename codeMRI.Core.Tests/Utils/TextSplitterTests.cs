using codeMRI.Core.Utils;

namespace codeMRI.Core.Tests.Utils;

[TestFixture]
public class TextSplitterTests
{
    [Test]
    public void Split_ShouldReturnEmpty_WhenTextIsEmpty()
    {
        var result = TextSplitter.Split("", 100, 10);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Split_ShouldReturnOneChunk_WhenTextIsShorterThanChunkSize()
    {
        var text = "Hello world";
        var result = TextSplitter.Split(text, 100, 10);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(text));
    }

    [Test]
    public void Split_ShouldReturnMultipleChunks_WhenTextIsLongerThanChunkSize()
    {
        var text = "0123456789";
        var result = TextSplitter.Split(text, 5, 2);

        // Chunk 1: "01234" (0 to 5)
        // Next start: 5 - 2 = 3
        // Chunk 2: "34567" (3 to 5)
        // Next start: 3 + 5 - 2 = 6
        // Chunk 3: "6789" (6 to 4)

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("01234"));
        Assert.That(result[1], Is.EqualTo("34567"));
        Assert.That(result[2], Is.EqualTo("6789"));
    }

    [Test]
    public void Split_ShouldHandleNoOverlap()
    {
        var text = "0123456789";
        var result = TextSplitter.Split(text, 5, 0);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("01234"));
        Assert.That(result[1], Is.EqualTo("56789"));
    }

    [Test]
    public void Split_ShouldThrow_WhenInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => TextSplitter.Split("test", 0, 0));
        Assert.Throws<ArgumentException>(() => TextSplitter.Split("test", 10, -1));
        Assert.Throws<ArgumentException>(() => TextSplitter.Split("test", 10, 10));
    }
}