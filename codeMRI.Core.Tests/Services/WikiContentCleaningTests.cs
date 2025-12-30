using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using codeMRI.Core.Services.MessageComposition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

/// <summary>
/// Tests for the new wiki link conversion and preamble stripping functionality
/// </summary>
[TestFixture]
public class WikiContentCleaningTests
{
    private readonly DocumentationRevisionService _revisionService;

    public WikiContentCleaningTests()
    {
        var mockFacade = new Mock<ILLMServiceFacade>();
        var mockLogger = new Mock<ILogger<DocumentationRevisionService>>();
        var mockOptions = Options.Create(new CodeWikiOptions());
        var markdownRepair = new MarkdownRepairService();
        var mermaidRepair = new MermaidRepairService();
        
        _revisionService = new DocumentationRevisionService(
            mockFacade.Object,
            mockLogger.Object,
            mockOptions,
            markdownRepair,
            mermaidRepair);
    }

    [Test]
    public void StripLLMPreamble_RemovesPreambleText()
    {
        // Arrange
        var content = @"Here's the refined parent-level documentation with enriched abstract descriptions:

```markdown
# Test Module

Content here
```";

        // Act - Access via reflection since the method is private
        var method = typeof(DocumentationRevisionService).GetMethod(
            "CleanRevisedContent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (string)method!.Invoke(_revisionService, new object[] { content, "Test Module" })!;

        // Assert
        Assert.That(result, Does.Not.Contain("Here's the refined"));
        Assert.That(result, Does.StartWith("# Test Module"));
    }

    [Test]
    public void ConvertWikiLinks_ConvertsSimpleWikiLinks()
    {
        // Arrange
        var content = @"# Test Module

This module uses [[BasicTests]] and [[NumberTests]] for validation.";

        // Act
        var method = typeof(DocumentationRevisionService).GetMethod(
            "CleanRevisedContent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (string)method!.Invoke(_revisionService, new object[] { content, "Test Module" })!;

        // Assert
        Assert.That(result, Does.Contain("[BasicTests](#basictests)"));
        Assert.That(result, Does.Contain("[NumberTests](#numbertests)"));
        Assert.That(result, Does.Not.Contain("[["));
    }

    [Test]
    public void ConvertWikiLinks_ConvertsWikiLinksWithDisplayText()
    {
        // Arrange
        var content = @"# Test Module

See [[SymbolTests|the symbol tests]] for more details.";

        // Act
        var method = typeof(DocumentationRevisionService).GetMethod(
            "CleanRevisedContent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (string)method!.Invoke(_revisionService, new object[] { content, "Test Module" })!;

        // Assert
        Assert.That(result, Does.Contain("[the symbol tests](#symboltests)"));
        Assert.That(result, Does.Not.Contain("[["));
    }

    [Test]
    public void CleanContent_HandlesComplexLLMOutput()
    {
        // Arrange - Simulates the exact problem from the user report
        var content = @"Here's the refined parent-level documentation with enriched abstract descriptions, concrete evidence, and improved cross-referencing:

```markdown
# net skiby elva tokenizer

## Overview
The `src/test/java/net/skiby/elva/tokenizer` module provides comprehensive validation for the Elva language tokenizer through four specialized test classes: [[BasicTests]], [[NumberTests]], [[SymbolTests]], and [[IdentifierTests]].
```";

        // Act
        var method = typeof(DocumentationRevisionService).GetMethod(
            "CleanRevisedContent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (string)method!.Invoke(_revisionService, new object[] { content, "net skiby elva tokenizer" })!;

        // Assert
        Assert.That(result, Does.Not.Contain("Here's the refined"));
        Assert.That(result, Does.Not.Contain("```markdown"));
        Assert.That(result, Does.StartWith("# net skiby elva tokenizer"));
        Assert.That(result, Does.Contain("[BasicTests](#basictests)"));
        Assert.That(result, Does.Contain("[NumberTests](#numbertests)"));
        Assert.That(result, Does.Contain("[SymbolTests](#symboltests)"));
        Assert.That(result, Does.Contain("[IdentifierTests](#identifiertests)"));
        Assert.That(result, Does.Not.Contain("[["));
    }
}
