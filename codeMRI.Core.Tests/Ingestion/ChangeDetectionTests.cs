using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Ingestion;

[TestFixture]
public class ChangeDetectionTests
{
    private string CalculateHash(string input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        using var sha512 = SHA512.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha512.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    [Test]
    public void CalculateHash_WhenSameInput_ReturnsSameHash()
    {
        // Arrange
        var input = "Hello World";

        // Act
        var hash1 = CalculateHash(input);
        var hash2 = CalculateHash(input);

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void CalculateHash_WhenDifferentInput_ReturnsDifferentHash()
    {
        // Arrange
        var input1 = "Hello World";
        var input2 = "Hello World!";

        // Act
        var hash1 = CalculateHash(input1);
        var hash2 = CalculateHash(input2);

        // Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public void CalculateHash_WhenEmptyString_ReturnsConsistentHash()
    {
        // Arrange
        var input = "";

        // Act
        var hash1 = CalculateHash(input);
        var hash2 = CalculateHash(input);

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2));
        Assert.That(hash1.Length, Is.EqualTo(128)); // SHA512 hash length in hex
    }

    [Test]
    public void CalculateHash_WhenWhitespaceDifferences_ReturnsDifferentHash()
    {
        // Arrange
        var input1 = "Hello World";
        var input2 = "Hello  World"; // Extra space
        var input3 = "Hello World\n"; // Newline
        var input4 = " Hello World"; // Leading space

        // Act
        var hash1 = CalculateHash(input1);
        var hash2 = CalculateHash(input2);
        var hash3 = CalculateHash(input3);
        var hash4 = CalculateHash(input4);

        // Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
        Assert.That(hash1, Is.Not.EqualTo(hash3));
        Assert.That(hash1, Is.Not.EqualTo(hash4));
        Assert.That(hash2, Is.Not.EqualTo(hash3));
        Assert.That(hash2, Is.Not.EqualTo(hash4));
        Assert.That(hash3, Is.Not.EqualTo(hash4));
    }

    [Test]
    public void CalculateHash_WhenCaseDifferences_ReturnsDifferentHash()
    {
        // Arrange
        var input1 = "Hello World";
        var input2 = "hello world";
        var input3 = "HELLO WORLD";

        // Act
        var hash1 = CalculateHash(input1);
        var hash2 = CalculateHash(input2);
        var hash3 = CalculateHash(input3);

        // Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
        Assert.That(hash1, Is.Not.EqualTo(hash3));
        Assert.That(hash2, Is.Not.EqualTo(hash3));
    }

    [Test]
    public void CalculateHash_WhenSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var inputs = new[]
        {
            "Hello\nWorld",
            "Hello\tWorld",
            "Hello\r\nWorld",
            "Hello \"World\"!",
            "Hello 'World'!",
            "Hello @#$%^&*()_+ World",
            "Hello 世界", // Unicode characters
            "Hello 🌍 World" // Emoji
        };

        // Act & Assert
        var hashes = inputs.Select(CalculateHash).ToList();
        
        // All hashes should be unique
        for (int i = 0; i < hashes.Count; i++)
        {
            for (int j = i + 1; j < hashes.Count; j++)
            {
                Assert.That(hashes[i], Is.Not.EqualTo(hashes[j]), 
                    $"Hashes for inputs {i} and {j} should be different");
            }
        }
    }

    [Test]
    public void CalculateHash_WhenLargeContent_HandlesEfficiently()
    {
        // Arrange
        var largeContent = string.Join("\n", Enumerable.Repeat("This is a line of content for testing.", 10000));

        // Act
        var startTime = DateTime.UtcNow;
        var hash = CalculateHash(largeContent);
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.That(hash, Is.Not.Null);
        Assert.That(hash.Length, Is.EqualTo(128));
        Assert.That(endTime.Subtract(startTime).TotalMilliseconds, Is.LessThan(1000)); // Should be fast
    }

    [Test]
    public void CalculateHash_WhenVerySimilarContent_ReturnsDifferentHash()
    {
        // Arrange
        var baseContent = "public class TestClass { public void Method() { } }";
        var variations = new[]
        {
            baseContent,
            baseContent + " ", // Extra space
            baseContent + "\n", // Extra newline
            baseContent.Replace("Method", "method"), // Case change
            baseContent.Replace("{", "{\n"), // Formatting change
            baseContent.Replace("()", "( )") // Space in parentheses
        };

        // Act
        var hashes = variations.Select(CalculateHash).ToList();

        // Assert
        for (int i = 0; i < hashes.Count; i++)
        {
            for (int j = i + 1; j < hashes.Count; j++)
            {
                Assert.That(hashes[i], Is.Not.EqualTo(hashes[j]), 
                    $"Hashes for variations {i} and {j} should be different");
            }
        }
    }

    [Test]
    public void CalculateHash_WhenNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CalculateHash(null!));
    }

    [Test]
    public void CalculateHash_WhenConsistentAcrossMultipleCalls_ReturnsSameResult()
    {
        // Arrange
        var input = "Consistent test input";

        // Act
        var hash1 = CalculateHash(input);
        var hash2 = CalculateHash(input);

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2));
        Assert.That(hash1.Length, Is.EqualTo(128)); // SHA512 produces 128-character hex string
    }

    [Test]
    public void CalculateHash_WhenJsonContent_HandlesCorrectly()
    {
        // Arrange
        var json1 = "{\"name\":\"test\",\"value\":123}";
        var json2 = "{\"name\": \"test\", \"value\": 123}"; // Different spacing
        var json3 = "{\"value\":123,\"name\":\"test\"}"; // Different order

        // Act
        var hash1 = CalculateHash(json1);
        var hash2 = CalculateHash(json2);
        var hash3 = CalculateHash(json3);

        // Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
        Assert.That(hash1, Is.Not.EqualTo(hash3));
        Assert.That(hash2, Is.Not.EqualTo(hash3));
    }

    [Test]
    public void CalculateHash_WhenCodeContent_HandlesCorrectly()
    {
        // Arrange
        var code1 = "public void Test() { Console.WriteLine(\"Hello\"); }";
        var code2 = "public void Test() {\n    Console.WriteLine(\"Hello\");\n}"; // Different formatting
        var code3 = "public void Test(){Console.WriteLine(\"Hello\");}"; // No spacing

        // Act
        var hash1 = CalculateHash(code1);
        var hash2 = CalculateHash(code2);
        var hash3 = CalculateHash(code3);

        // Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
        Assert.That(hash1, Is.Not.EqualTo(hash3));
        Assert.That(hash2, Is.Not.EqualTo(hash3));
    }
}
