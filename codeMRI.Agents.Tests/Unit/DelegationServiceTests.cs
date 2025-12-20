using codeMRI.Agents.Configuration;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Agents.Tests.Unit;

[TestFixture]
public class DelegationServiceTests
{
    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<DelegationService>>();
    }

    private Mock<ILogger<DelegationService>> _mockLogger = null!;
    private DelegationService _service = null!;

    private DelegationService CreateService(AgentSettings settings)
    {
        var mockSettings = new Mock<IOptions<AgentSettings>>();
        mockSettings.Setup(s => s.Value).Returns(settings);
        var mockMessageBus = new Mock<AgentMessageBus>(Mock.Of<ILogger<AgentMessageBus>>());
        return new DelegationService(_mockLogger.Object, mockSettings.Object, mockMessageBus.Object);
    }

    [Test]
    public void ShouldDelegate_ReturnsFalse_WhenDelegationIsDisabled()
    {
        // Arrange
        var settings = new AgentSettings
        {
            EnableDelegation = false,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            }
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = new CodeComponent
            {
                Name = "TestComponent",
                ComplexityScore = 10, // Above threshold
                LineCount = 600 // Above threshold
            }
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldDelegate_ReturnsFalse_WhenComplexityBelowThreshold()
    {
        // Arrange
        var settings = new AgentSettings
        {
            EnableDelegation = true,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            }
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = new CodeComponent
            {
                Name = "SimpleComponent",
                ComplexityScore = 5,
                LineCount = 100
            }
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldDelegate_ReturnsTrue_WhenComplexityExceedsThreshold()
    {
        // Arrange
        var settings = new AgentSettings
        {
            EnableDelegation = true,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            }
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = new CodeComponent
            {
                Name = "ComplexComponent",
                ComplexityScore = 10,
                LineCount = 100
            }
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldDelegate_ReturnsTrue_WhenLineCountExceedsThreshold()
    {
        // Arrange
        var settings = new AgentSettings
        {
            EnableDelegation = true,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            }
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = new CodeComponent
            {
                Name = "LargeComponent",
                ComplexityScore = 5,
                LineCount = 600
            }
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldDelegate_UsesCustomThresholds()
    {
        // Arrange
        var settings = new AgentSettings
        {
            EnableDelegation = true,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 5 }, // Lower threshold
                { "LineCount", 300 } // Lower threshold
            }
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = new CodeComponent
            {
                Name = "Component",
                ComplexityScore = 6, // Would be OK with default (8), but exceeds custom (5)
                LineCount = 100
            }
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldDelegate_UsesDefaultValues_WhenThresholdsNotSet()
    {
        // Arrange - Settings with missing threshold keys
        var settings = new AgentSettings
        {
            EnableDelegation = true,
            ComplexityThresholds = new Dictionary<string, int>() // Empty
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = new CodeComponent
            {
                Name = "Component",
                ComplexityScore = 9, // Above default (8)
                LineCount = 100
            }
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.True); // Should use default threshold of 8
    }

    [Test]
    public void ShouldDelegate_ReturnsFalse_WhenPayloadIsNotCodeComponent()
    {
        // Arrange
        var settings = new AgentSettings
        {
            EnableDelegation = true,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            }
        };
        _service = CreateService(settings);

        var task = new AgentTask
        {
            Type = "Test",
            Payload = "SomeOtherPayload"
        };

        // Act
        var result = _service.ShouldDelegate(task, new object());

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void SplitTask_SplitsModuleNodeIntoSubTasks()
    {
        // Arrange
        var settings = new AgentSettings { EnableDelegation = true };
        _service = CreateService(settings);

        var child1 = new ModuleNode { Id = "child1", Name = "Child1" };
        var child2 = new ModuleNode { Id = "child2", Name = "Child2" };

        var moduleNode = new ModuleNode
        {
            Id = "parent",
            Name = "Parent",
            Children = new List<ModuleNode> { child1, child2 }
        };

        var originalTask = new AgentTask
        {
            Id = "parent-task",
            Type = "Process",
            Payload = moduleNode,
            Metadata = new Dictionary<string, string> { { "Key", "Value" } }
        };

        // Act
        var subTasks = _service.SplitTask(originalTask);

        // Assert
        Assert.That(subTasks, Has.Count.EqualTo(2));
        Assert.That(subTasks[0].Payload, Is.EqualTo(child1));
        Assert.That(subTasks[1].Payload, Is.EqualTo(child2));
        Assert.That(subTasks[0].Type, Is.EqualTo("Process"));
        Assert.That(subTasks[1].Type, Is.EqualTo("Process"));
        Assert.That(subTasks[0].Metadata["ParentId"], Is.EqualTo("parent-task"));
        Assert.That(subTasks[1].Metadata["ParentId"], Is.EqualTo("parent-task"));
    }
}