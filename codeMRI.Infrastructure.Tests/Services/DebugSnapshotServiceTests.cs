using codeMRI.Core.Models;
using codeMRI.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace codeMRI.Infrastructure.Tests.Services;

[TestFixture]
public class DebugSnapshotServiceTests
{
    private Mock<ILogger<DebugSnapshotService>> _mockLogger = null!;
    private DebugSnapshotService _service = null!;
    private string _tempRepoPath = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<DebugSnapshotService>>();
        _service = new DebugSnapshotService(_mockLogger.Object);
        _tempRepoPath = Path.Combine(Path.GetTempPath(), "codeMRI_test_repo_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRepoPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRepoPath))
        {
            Directory.Delete(_tempRepoPath, true);
        }
    }

    [Test]
    public async Task DeleteSnapshotsAsync_ShouldDeleteDebugDirectory()
    {
        // Arrange
        var debugDir = Path.Combine(_tempRepoPath, ".codemri/debug");
        Directory.CreateDirectory(debugDir);
        var testFile = Path.Combine(debugDir, "snapshot_test.json");
        await File.WriteAllTextAsync(testFile, "{}");

        // Act
        await _service.DeleteSnapshotsAsync(_tempRepoPath);

        // Assert
        Assert.That(Directory.Exists(debugDir), Is.False);
    }

    [Test]
    public void DeleteSnapshotsAsync_WhenDirectoryDoesNotExist_ShouldNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _service.DeleteSnapshotsAsync(_tempRepoPath));
    }
}
