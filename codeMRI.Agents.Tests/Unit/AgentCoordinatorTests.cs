using codeMRI.Agents.Interfaces;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Agents.Tests.Unit;

[TestFixture]
public class AgentCoordinatorTests
{
    private Mock<IAgent> _mockAgent = null!;
    private Mock<ILogger<AgentCoordinator>> _mockLogger = null!;
    private Mock<ILogger<DelegationService>> _mockDelegationLogger = null!;
    private AgentCoordinator _coordinator = null!;
    private DelegationService _delegationService = null!;

    [SetUp]
    public void Setup()
    {
        _mockAgent = new Mock<IAgent>();
        _mockLogger = new Mock<ILogger<AgentCoordinator>>();
        _mockDelegationLogger = new Mock<ILogger<DelegationService>>();
        _delegationService = new DelegationService(_mockDelegationLogger.Object);
        
        _coordinator = new AgentCoordinator(
            _delegationService,
            _mockLogger.Object,
            new[] { _mockAgent.Object }
        );
    }

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
}