using codeMRI.Agents.Agents;
using codeMRI.Agents.Models;
using codeMRI.Agents.Services;
using codeMRI.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Agents.Tests.Unit;

[TestFixture]
public class BaseAgentTests
{
    [SetUp]
    public void Setup()
    {
        _mockAstService = new Mock<IASTServiceClient>();
        _mockLogger = new Mock<ILogger<BaseAgent>>();
        _mockMessageBus = new Mock<AgentMessageBus>(MockBehavior.Strict, Mock.Of<ILogger<AgentMessageBus>>());

        _agent = new TestAgent(_mockMessageBus.Object, _mockLogger.Object, _mockAstService.Object);
    }

    private Mock<IASTServiceClient> _mockAstService = null!;
    private Mock<ILogger<BaseAgent>> _mockLogger = null!;
    private Mock<AgentMessageBus> _mockMessageBus = null!;
    private TestAgent _agent = null!;

    [Test]
    public void CanHandle_ShouldReturnTrue_WhenTaskMatchesAgentRole()
    {
        // Arrange
        var task = new AgentTask { Type = "TestAgent" };

        // Act
        var result = _agent.CanHandle(task);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_ShouldReturnFalse_WhenTaskDoesNotMatchAgentRole()
    {
        // Arrange
        var task = new AgentTask { Type = "DifferentAgent" };

        // Act
        var result = _agent.CanHandle(task);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldDelegate_ShouldReturnNull_WhenTaskHasNoPayload()
    {
        // Arrange
        var task = new AgentTask { Type = "TestAgent" };

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ShouldDelegate_ShouldReturnNull_WhenTaskAlreadyDelegated()
    {
        // Arrange
        var task = new AgentTask
        {
            Type = "TestAgent",
            Payload = "test code",
            Metadata = new Dictionary<string, string> { ["IsDelegated"] = "true" }
        };

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ShouldDelegate_ShouldReturnNull_WhenComplexityBelowThresholds()
    {
        // Arrange
        var simpleCode = "console.log('hello');"; // ~20 tokens, complexity 0
        var task = new AgentTask { Type = "TestAgent", Payload = simpleCode };

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ShouldDelegate_ShouldReturnRequest_WhenTokenCountExceedsMax()
    {
        // Arrange
        var largeCode = new string('x', 10000); // ~2500 tokens
        var task = new AgentTask { Type = "TestAgent", Payload = largeCode };

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Reason, Contains.Substring("TokenCount"));
        Assert.That(result!.TargetAgentType, Is.EqualTo("TestAgent"));
    }

    [Test]
    public async Task ShouldDelegate_ShouldReturnRequest_WhenCyclomaticComplexityExceedsMax()
    {
        // Arrange - Create code with multiple if statements on separate lines
        var complexCode = """
                                      if (true) { }
                                      if (false) { }
                                      for (int i=0; i<10; i++) { }
                                      while (true) { }
                                      switch (x) {
                                          case 1: break;
                                          case 2: break;
                                      }
                                      try { } catch { }
                                      if (a && b) { }
                                      if (c || d) { }
                                      if (e) { }
                                      if (f) { }
                                      if (g) { }
                                      if (h) { }
                          """; // Total complexity: 12, exceeds MaxComplexity=10
        var task = new AgentTask { Type = "TestAgent", Payload = complexCode };

        // Act
        var result = await _agent.ShouldDelegate(task, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Reason, Contains.Substring("CyclomaticComplexity"));
    }

    [Test]
    public async Task CalculateComplexity_ShouldUseAstService_WhenAvailable()
    {
        // Arrange
        var code = "public class Test { public void Method() { } }";
        var astResult = new ASTParseResult { Language = "csharp", Metrics = new object() };
        _mockAstService.Setup(s => s.ParseCodeAsync(code, "csharp", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(astResult);

        // Act
        var result = await _agent.CalculateComplexityInternal(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockAstService.Verify(s => s.ParseCodeAsync(code, "csharp", "", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CalculateComplexity_ShouldFallback_WhenAstServiceFails()
    {
        // Arrange
        var code = "if (true) { return; }";
        _mockAstService.Setup(s => s.ParseCodeAsync(code, "csharp", "", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        // Act
        var result = await _agent.CalculateComplexityInternal(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CyclomaticComplexity, Is.GreaterThan(0)); // Should detect 'if'
    }

    [Test]
    public void CreateDelegationRequest_ShouldMarkAsDelegated()
    {
        // Arrange
        var task = new AgentTask { Id = "original", Type = "TestAgent" };

        // Act
        var request = _agent.CreateDelegationRequestInternal(task, "TestReason", 100);

        // Assert
        Assert.That(request.TargetAgentType, Is.EqualTo("TestAgent"));
        Assert.That(request.SubTask.Id, Is.Not.EqualTo("original"));
        Assert.That(request.SubTask.Metadata["IsDelegated"], Is.EqualTo("true"));
        Assert.That(request.SubTask.Metadata["ParentTaskId"], Is.EqualTo("original"));
    }

    private class TestAgent : BaseAgent
    {
        public TestAgent(AgentMessageBus messageBus, ILogger logger, IASTServiceClient? astService = null)
            : base(messageBus, logger, astService)
        {
        }

        public override string Role => "TestAgent";

        public override Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentResult { Success = true });
        }

        public Task<CodeComplexityMetrics> CalculateComplexityInternal(string code, CancellationToken cancellationToken)
        {
            return base.CalculateComplexity(code, cancellationToken);
        }

        public DelegationRequest CreateDelegationRequestInternal(AgentTask task, string reasonType, int value)
        {
            return base.CreateDelegationRequest(task, reasonType, value);
        }
    }
}