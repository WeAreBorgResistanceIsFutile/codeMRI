using codeMRI.Core.Interfaces;
using codeMRI.Core.Services;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

/// <summary>
///     Tests for MermaidRepairService following TDD principles
///     Starting generic, progressively getting more specific
/// </summary>
[TestFixture]
public class MermaidRepairServiceTests
{
    private IMermaidRepairService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new MermaidRepairService();
    }

    #region Generic Tests - ExtractMermaid

    [Test]
    public void ExtractMermaid_WithValidMermaid_ReturnsMermaid()
    {
        // Arrange - most generic case: valid Mermaid in code block
        var input = """
                    ```mermaid
                    graph TD
                        A --> B
                    ```
                    """;

        // Act
        var result = _service.ExtractMermaid(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("graph TD\n    A --> B"));
    }

    [Test]
    public void ExtractMermaid_WithChattyResponse_ExtractsMermaid()
    {
        // Arrange - more specific: Mermaid with text before and after
        var input = """
                    Sure! Here's your diagram:

                    ```mermaid
                    graph TD
                        A --> B
                    ```

                    Hope this helps!
                    """;

        // Act
        var result = _service.ExtractMermaid(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("graph TD\n    A --> B"));
    }

    [Test]
    public void ExtractMermaid_NoMermaidBlock_ReturnsNull()
    {
        // Arrange
        var input = "This is just text without any mermaid block.";

        // Act
        var result = _service.ExtractMermaid(input);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region Mermaid Repair Tests

    [Test]
    public void RepairMermaid_WithSpecialCharactersInLabels_QuotesLabels()
    {
        // Arrange
        var input = """
                    graph TD
                        A[Input String] --> B[Tokenizer]
                        E1[Value: e.g., "10,5", "+"]
                        E3[Position: start="0" end="3"]
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain("E1[\"Value: e.g., \\\"10,5\\\", \\\"+\\\"\"]"));
        Assert.That(result, Does.Contain("E3[\"Position: start=\\\"0\\\" end=\\\"3\\\"\"]"));
    }

    [Test]
    public void RepairMermaid_WithArrowLabels_QuotesLabels()
    {
        // Arrange
        var input = """
                    graph TD
                        A -->|Defines Math Op: PLUS| B
                        C ---|Uses Character Classification: isSymbol()| D
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain(@"|""Defines Math Op: PLUS""|"));
        Assert.That(result, Does.Contain(@"|""Uses Character Classification: isSymbol()""|"));
    }

    [Test]
    public void RepairMermaid_WithNestedBrackets_QuotesLabels()
    {
        // Arrange
        var input = """
                    graph TD
                        A -->|returns| TokenList[Token[]]
                        B -->|returns| List[string]
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain(@"TokenList[""Token[]""]"));
        Assert.That(result, Does.Contain(@"List[""string""]"));
    }
    
    [Test]
    public void RepairMermaid_1()
    {
        // Arrange
        var input = """
                    graph TD
                    A["Start"] --> B["Identify current char type via TokenizerUtils"]
                    B --> C["Peek next char using peek(1")]
                    C --> D{"Type transition?"}
                    D -->"|"Yes"| E[Check special patterns"]
                    E --> F{"Valid pattern?"}
                    F -->"|"Yes"| G[Tokenize buffer via tokenizeBufferContents(")]
                    F -->"|"No"| H[Continue accumulation"]
                    D -->|"No"| H
                    G --> I["Reset buffer via resetBuffer(")]
                    H --> J["Accumulate character to buffer"]
                    J --> K["Advance currentPosition"]
                    K --> B
                    """;

        // Act
        var result = _service.RepairMermaid(input);

        // Assert
        Assert.That(result, Does.Contain("""C["Peek next char using peek(1)"]"""));
        Assert.That(result, Does.Contain("""-->|"Yes"| E"""));
        Assert.That(result, Does.Contain("""-->|"No"| H"""));
        Assert.That(result, Does.Contain("""["Reset buffer via resetBuffer()"]"""));
    }

    #endregion
}
