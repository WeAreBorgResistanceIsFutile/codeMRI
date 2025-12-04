using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Core.Services;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Services;

[TestFixture]
public class ReferenceManagementServiceTests
{
    private IReferenceManagementService _service;

    [SetUp]
    public void Setup()
    {
        _service = new ReferenceManagementService();
    }

    [Test]
    public void RegisterComponent_ShouldAddComponentToRegistry()
    {
        // Arrange
        var id = "comp-1";
        var name = "TestComponent";
        var filePath = "/src/TestComponent.cs";
        var docPath = "/docs/TestComponent.md";

        // Act
        _service.RegisterComponent(id, name, filePath, docPath);

        // Assert
        var component = _service.GetComponent(id);
        Assert.That(component, Is.Not.Null);
        Assert.That(component.Name, Is.EqualTo(name));
        Assert.That(component.FilePath, Is.EqualTo(filePath));
        Assert.That(component.DocPath, Is.EqualTo(docPath));
    }

    [Test]
    public void RegisterComponent_ShouldUpdateExistingComponent_WhenIdExists()
    {
        // Arrange
        var id = "comp-1";
        _service.RegisterComponent(id, "OldName", "oldpath", "olddoc");

        // Act
        _service.RegisterComponent(id, "NewName", "newpath", "newdoc");

        // Assert
        var component = _service.GetComponent(id);
        Assert.That(component!.Name, Is.EqualTo("NewName"));
        Assert.That(component!.DocPath, Is.EqualTo("newdoc"));
    }

    [Test]
    public void ResolveLink_ShouldReturnDocPath_WhenComponentExists()
    {
        // Arrange
        var id = "comp-1";
        var docPath = "/docs/MyDoc.md";
        _service.RegisterComponent(id, "MyComponent", "/src/MyComp.cs", docPath);

        // Act
        var result = _service.ResolveLink(id);

        // Assert
        Assert.That(result, Is.EqualTo(docPath));
    }

    [Test]
    public void ResolveLink_ShouldReturnNull_WhenComponentDoesNotExist()
    {
        // Act
        var result = _service.ResolveLink("non-existent-id");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindReferences_ShouldIdentifyComponentNamesInText()
    {
        // Arrange
        _service.RegisterComponent("id-1", "UserService", "/src/UserService.cs", "docs/UserService.md");
        _service.RegisterComponent("id-2", "Logger", "/src/Logger.cs", "docs/Logger.md");
        
        var content = "The UserService uses the Logger to record events.";
        var sourceId = "id-3"; // Some other component

        // Act
        var references = _service.FindReferences(content, sourceId).ToList();

        // Assert
        Assert.That(references, Has.Count.EqualTo(2));
        Assert.That(references.Any(r => r.TargetName == "UserService"), Is.True);
        Assert.That(references.Any(r => r.TargetName == "Logger"), Is.True);
    }

    [Test]
    public void FindReferences_ShouldIgnoreSelfReference()
    {
        // Arrange
        var id = "id-1";
        var name = "SelfAwareComponent";
        _service.RegisterComponent(id, name, "/src/SAC.cs", "docs/SAC.md");

        var content = "This is the SelfAwareComponent logic.";

        // Act
        var references = _service.FindReferences(content, id);

        // Assert
        Assert.That(references, Is.Empty);
    }

    [Test]
    public void EnrichContentWithLinks_ShouldReplaceNamesWithMarkdownLinks()
    {
        // Arrange
        _service.RegisterComponent("id-1", "DataProcessor", "/src/DataProcessor.cs", "docs/DataProcessor.md");
        var content = "The DataProcessor handles the input.";
        var expected = "The [DataProcessor](docs/DataProcessor.md) handles the input.";

        // Act
        var result = _service.EnrichContentWithLinks(content, "source-id");

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void EnrichContentWithLinks_ShouldHandleMultipleOccurrences()
    {
        // Arrange
        _service.RegisterComponent("id-1", "Cache", "/src/Cache.cs", "docs/Cache.md");
        var content = "Cache strategy. Using the Cache.";
        var expected = "[Cache](docs/Cache.md) strategy. Using the [Cache](docs/Cache.md).";

        // Act
        var result = _service.EnrichContentWithLinks(content, "source-id");

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void RegisterRelationship_ShouldTrackRelatedIds()
    {
        // Arrange
        var sourceId = "src-1";
        var targetId = "target-1";
        _service.RegisterComponent(sourceId, "Source", "path", "doc");
        _service.RegisterComponent(targetId, "Target", "path", "doc");

        // Act
        _service.RegisterRelationship(sourceId, targetId, EdgeType.Dependency);

        // Assert
        var source = _service.GetComponent(sourceId);
        Assert.That(source!.RelatedComponentIds, Contains.Item(targetId));
    }
    
    [Test]
    public void EnrichContentWithLinks_ShouldPrioritizeLongerNames()
    {
        // Arrange: "SuperUser" vs "User". 
        // If content contains "SuperUser", we shouldn't replace "User" inside it if "SuperUser" is also a component.
        // NOTE: Simple replacement might break links if not careful.
        // Example: "User" -> [User](...)
        // "SuperUser" -> Super[User](...) if "User" is processed first and "SuperUser" is not detected or handled poorly.
        
        _service.RegisterComponent("id-1", "User", "path", "docs/User.md");
        _service.RegisterComponent("id-2", "SuperUser", "path", "docs/SuperUser.md");
        
        var content = "The SuperUser inherits from User.";
        
        // Act
        var result = _service.EnrichContentWithLinks(content, "source-id");
        
        // Assert
        // Ideally: "The [SuperUser](docs/SuperUser.md) inherits from [User](docs/User.md)."
        // If naive replace "User" first: "The Super[User](docs/User.md) inherits from [User](docs/User.md)." (Bad)
        // If naive replace "SuperUser" first: "The [SuperUser](docs/SuperUser.md) inherits from User." -> Then replace User -> "The [SuperUser](docs/SuperUser.md) inherits from [User](docs/User.md)." (Good)
        
        Assert.That(result, Contains.Substring("[SuperUser](docs/SuperUser.md)"));
        Assert.That(result, Contains.Substring("[User](docs/User.md)"));
    }
}
