using codeMRI.Agents.Agents;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Agents.Tests.Unit;

public class DelegationLogicTests
{
    private TestAgent _agent = null!;
    private Mock<IASTServiceClient> _astServiceMock = null!;
    private Mock<ILogger<TestAgent>> _loggerMock = null!;
    private Mock<AgentMessageBus> _messageBusMock = null!;

    [SetUp]
    public void Setup()
    {
        _messageBusMock = new Mock<AgentMessageBus>(Mock.Of<ILogger<AgentMessageBus>>());
        _loggerMock = new Mock<ILogger<TestAgent>>();
        _astServiceMock = new Mock<IASTServiceClient>();

        _agent = new TestAgent(_messageBusMock.Object, _loggerMock.Object, _astServiceMock.Object);
    }

    [Test]
    public async Task ShouldDelegate_ReturnsNull_WhenComplexityIsLow()
    {
        // Arrange
        var code = "public void Hello() { Console.WriteLine(\"Hi\"); }"; // Simple code
        var task = new AgentTask
        {
            Type = "Test",
            Payload = code
        };

        // Mock AST service to return low complexity
        // We assume CalculateComplexity uses AST service or internal logic
        // For this test, we'll assume the BaseAgent implementation uses a basic heuristic if AST is missing or returns simple data
        // But if we inject AST, we should mock it.

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public async Task ShouldDelegate_ReturnsRequest_WhenTokenCountIsHigh()
    {
        // Arrange
        // Generate a long string to trigger token count threshold (assuming 2000 tokens approx 8000 chars)
        var longCode = new string('a', 10000);
        var task = new AgentTask
        {
            Type = "Test",
            Payload = longCode
        };

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.That(result!.Reason, Does.StartWith("Delegation required due to high complexity (TokenCount"));
    }

    public class TestAgent : BaseAgent
    {
        public TestAgent(
            AgentMessageBus messageBus,
            ILogger logger,
            IASTServiceClient? astService = null)
            : base(messageBus, logger, astService)
        {
        }

        public override string Role => "Test";

        public override Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentResult { Success = true });
        }
    }
}