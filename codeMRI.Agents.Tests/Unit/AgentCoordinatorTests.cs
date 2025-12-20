using codeMRI.Agents.Configuration;
using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace codeMRI.Agents.Tests.Unit;

[TestFixture]
public class AgentCoordinatorTests
{
    [SetUp]
    public void Setup()
    {
        _mockAgent = new Mock<IAgent>();
        _mockLogger = new Mock<ILogger<AgentCoordinator>>();
        _mockDelegationLogger = new Mock<ILogger<DelegationService>>();

        // Setup AgentSettings mock with default values
        var mockSettings = new Mock<IOptions<AgentSettings>>();
        mockSettings.Setup(s => s.Value).Returns(new AgentSettings
        {
            EnableDelegation = true,
            MaxRecursionDepth = 3,
            ComplexityThresholds = new Dictionary<string, int>
            {
                { "ComplexityScore", 8 },
                { "LineCount", 500 }
            }
        });

        var mockMessageBus = new Mock<AgentMessageBus>(Mock.Of<ILogger<AgentMessageBus>>());
        _delegationService =
            new DelegationService(_mockDelegationLogger.Object, mockSettings.Object, mockMessageBus.Object);

        _coordinator = new AgentCoordinator(
            _delegationService,
            _mockLogger.Object,
            new[] { _mockAgent.Object }
        );
    }

    private Mock<IAgent> _mockAgent = null!;
    private Mock<ILogger<AgentCoordinator>> _mockLogger = null!;
    private Mock<ILogger<DelegationService>> _mockDelegationLogger = null!;
    private AgentCoordinator _coordinator = null!;
    private DelegationService _delegationService = null!;

    [Test]
    public async Task CoordinateTaskAsync_ShouldExecuteAgent_WhenCanHandleReturnsTrue()
    {
        // Arrange
        var task = new AgentTask { Type = "TestAgent" };
        var expectedResult = new AgentResult { Success = true };

        _mockAgent.Setup(a => a.CanHandle(task)).Returns(true);
        _mockAgent.Setup(a => a.ShouldDelegate(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DelegationRequest?)null);
        _mockAgent.Setup(a => a.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.CoordinateTaskAsync(task, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _mockAgent.Verify(a => a.ExecuteAsync(task, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CoordinateTaskAsync_ShouldFail_WhenNoAgentCanHandle()
    {
        // Arrange
        var task = new AgentTask { Type = "Unknown" };
        _mockAgent.Setup(a => a.CanHandle(task)).Returns(false);

        // Act
        var result = await _coordinator.CoordinateTaskAsync(task, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task CoordinateTaskAsync_ShouldDelegate_WhenAgentRequestsDelegation()
    {
        // Arrange
        var originalTask = new AgentTask { Type = "TestAgent", Id = "task1" };
        var subTask = new AgentTask { Type = "TestAgent", Id = "task2" };
        var delegationRequest = new DelegationRequest
        {
            TaskId = "task1",
            TargetAgentType = "TestAgent",
            SubTask = subTask,
            Reason = "Complexity exceeded"
        };
        var expectedResult = new AgentResult { Success = true };

        _mockAgent.Setup(a => a.CanHandle(It.IsAny<AgentTask>())).Returns(true);
        _mockAgent.Setup(a => a.ShouldDelegate(originalTask, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delegationRequest);
        _mockAgent.Setup(a => a.ShouldDelegate(subTask, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DelegationRequest?)null);
        _mockAgent.Setup(a => a.ExecuteAsync(subTask, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.CoordinateTaskAsync(originalTask, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _mockAgent.Verify(a => a.ExecuteAsync(subTask, It.IsAny<CancellationToken>()), Times.Once);
        _mockAgent.Verify(a => a.ExecuteAsync(originalTask, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CoordinateTaskAsync_ShouldHandleRecursiveDelegation()
    {
        // Arrange - Multiple levels of delegation
        var task1 = new AgentTask { Type = "TestAgent", Id = "task1" };
        var task2 = new AgentTask { Type = "TestAgent", Id = "task2" };
        var task3 = new AgentTask { Type = "TestAgent", Id = "task3" };

        var delegationRequest1 = new DelegationRequest
        {
            TaskId = "task1",
            TargetAgentType = "TestAgent",
            SubTask = task2,
            Reason = "First delegation"
        };

        var delegationRequest2 = new DelegationRequest
        {
            TaskId = "task2",
            TargetAgentType = "TestAgent",
            SubTask = task3,
            Reason = "Second delegation"
        };

        var expectedResult = new AgentResult { Success = true };

        _mockAgent.Setup(a => a.CanHandle(It.IsAny<AgentTask>())).Returns(true);
        _mockAgent.Setup(a => a.ShouldDelegate(task1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delegationRequest1);
        _mockAgent.Setup(a => a.ShouldDelegate(task2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delegationRequest2);
        _mockAgent.Setup(a => a.ShouldDelegate(task3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DelegationRequest?)null);
        _mockAgent.Setup(a => a.ExecuteAsync(task3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.CoordinateTaskAsync(task1, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _mockAgent.Verify(a => a.ExecuteAsync(task3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CoordinateTaskAsync_ShouldContinueOnFailedDelegationCheck()
    {
        // Arrange
        var task = new AgentTask { Type = "TestAgent" };
        var expectedResult = new AgentResult { Success = true };

        _mockAgent.Setup(a => a.CanHandle(task)).Returns(true);
        _mockAgent.Setup(a => a.ShouldDelegate(task, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Delegation check failed"));
        _mockAgent.Setup(a => a.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.CoordinateTaskAsync(task, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _mockAgent.Verify(a => a.ExecuteAsync(task, It.IsAny<CancellationToken>()), Times.Once);
    }
}