using NUnit.Framework;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class MermaidSanitizationTests
{
    private Mock<ILLMServiceFacade> _mockLlmFacade;
    private Mock<ILogger<DocumentationRevisionService>> _mockLogger;
    private DocumentationRevisionService _service;

    [SetUp]
    public void Setup()
    {
        _mockLlmFacade = new Mock<ILLMServiceFacade>();
        _mockLogger = new Mock<ILogger<DocumentationRevisionService>>();
        var options = Options.Create(new CodeWikiOptions());
        _service = new DocumentationRevisionService(_mockLlmFacade.Object, _mockLogger.Object, options);
    }

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldSanitizeMermaidDiagrams()
    {
        // Arrange
        var parentPage = new WikiPage { Id = "1", Title = "TokenizerModule", Content = "# TokenizerModule" };
        var parentModule = new ModuleNode { Id = "mod-1", Name = "TokenizerModule" };
        var childPages = new List<WikiPage> { new() { Title = "Child", Content = "Content" } };

        var mermaidContent = @"# TokenizerModule

```mermaid
graph TD
    A[Input String] --> B[Tokenizer]
    E1[Value: e.g., ""10,5"", ""+""]
    E3[Position: start=""0"" end=""3""]
```";

        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = mermaidContent, StrategyUsed = "Simple" });

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert
        // We expect the labels to be properly quoted if they contain problematic characters
        // Note: the inner quotes are escaped with backslashes
        Assert.That(result.Content, Does.Contain("E1[\"Value: e.g., \\\"10,5\\\", \\\"+\\\"\"]"));
        Assert.That(result.Content, Does.Contain("E3[\"Position: start=\\\"0\\\" end=\\\"3\\\"\"]"));
    }

    [Test]
    public async Task ReviseParentDocumentationAsync_ShouldSanitizeArrowLabels()
    {
        // Arrange
        var parentPage = new WikiPage { Id = "1", Title = "ArrowModule", Content = "# ArrowModule" };
        var parentModule = new ModuleNode { Id = "mod-1", Name = "ArrowModule" };
        var childPages = new List<WikiPage> { new() { Title = "Child", Content = "Content" } };

        var mermaidContent = @"# ArrowModule

```mermaid
graph TD
    A -->|Defines Math Op: PLUS| B
    C ---|Uses Character Classification: isSymbol()| D
```";

        _mockLlmFacade.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<MessageCompositionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = mermaidContent, StrategyUsed = "Simple" });

        // Act
        var result = await _service.ReviseParentDocumentationAsync(parentPage, parentModule, childPages);

        // Assert
        Assert.That(result.Content, Does.Contain(@"|""Defines Math Op: PLUS""|"));
        Assert.That(result.Content, Does.Contain(@"|""Uses Character Classification: isSymbol()""|"));
    }
}
