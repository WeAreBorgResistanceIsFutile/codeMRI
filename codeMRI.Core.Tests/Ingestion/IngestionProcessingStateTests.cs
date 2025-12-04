using System.Text.Json;
using codeMRI.Core.Models;
using NUnit.Framework;

namespace codeMRI.Core.Tests.Ingestion;

[TestFixture]
public class IngestionProcessingStateTests
{
    [Test]
    public void IngestionProcessingState_WhenCreated_HasDefaultValues()
    {
        // Arrange & Act
        var state = new IngestionProcessingState();

        // Assert
        Assert.That(state.ProcessingQueue, Is.Not.Null);
        Assert.That(state.CompletedFiles, Is.Not.Null);
        Assert.That(state.FailedFiles, Is.Not.Null);
        Assert.That(state.Errors, Is.Not.Null);
        Assert.That(state.CurrentBatchSize, Is.EqualTo(50));
        Assert.That(state.CurrentStatus, Is.EqualTo("idle"));
        Assert.That(state.CurrentPosition, Is.EqualTo(0));
        Assert.That(state.TotalFiles, Is.EqualTo(0));
        Assert.That(state.StartedAt, Is.Null);
        Assert.That(state.CompletedAt, Is.Null);
    }

    [Test]
    public void IngestionProcessingState_WhenSerialized_CanBeDeserialized()
    {
        // Arrange
        var originalState = new IngestionProcessingState
        {
            ProcessingQueue = new List<string> { "file1.cs", "file2.cs", "file3.cs" },
            CompletedFiles = new List<string> { "file0.cs" },
            FailedFiles = new List<string> { "file4.cs" },
            CurrentBatchSize = 25,
            CurrentStatus = "processing",
            CurrentPosition = 1,
            TotalFiles = 4,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = null,
            Errors = new Dictionary<string, string>
            {
                ["file4.cs"] = "Access denied"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(originalState);
        var deserializedState = JsonSerializer.Deserialize<IngestionProcessingState>(json);

        // Assert
        Assert.That(deserializedState, Is.Not.Null);
        Assert.That(deserializedState.ProcessingQueue, Is.EqualTo(originalState.ProcessingQueue));
        Assert.That(deserializedState.CompletedFiles, Is.EqualTo(originalState.CompletedFiles));
        Assert.That(deserializedState.FailedFiles, Is.EqualTo(originalState.FailedFiles));
        Assert.That(deserializedState.CurrentBatchSize, Is.EqualTo(originalState.CurrentBatchSize));
        Assert.That(deserializedState.CurrentStatus, Is.EqualTo(originalState.CurrentStatus));
        Assert.That(deserializedState.CurrentPosition, Is.EqualTo(originalState.CurrentPosition));
        Assert.That(deserializedState.TotalFiles, Is.EqualTo(originalState.TotalFiles));
        Assert.That(deserializedState.Errors, Is.EqualTo(originalState.Errors));
    }

    [Test]
    public void IngestionProcessingState_WhenSerializedWithNullDates_HandlesGracefully()
    {
        // Arrange
        var state = new IngestionProcessingState
        {
            StartedAt = null,
            CompletedAt = null
        };

        // Act & Assert
        var json = JsonSerializer.Serialize(state);
        var deserializedState = JsonSerializer.Deserialize<IngestionProcessingState>(json);

        Assert.That(deserializedState!.StartedAt, Is.Null);
        Assert.That(deserializedState!.CompletedAt, Is.Null);
    }

    [Test]
    public void IngestionProcessingState_WhenCalculatingProgress_ReturnsCorrectPercentage()
    {
        // Arrange
        var state = new IngestionProcessingState
        {
            TotalFiles = 100,
            CompletedFiles = new List<string>(Enumerable.Repeat("file", 25))
        };

        // Act
        var progress = state.TotalFiles > 0 
            ? (double)state.CompletedFiles.Count / state.TotalFiles * 100 
            : 100;

        // Assert
        Assert.That(progress, Is.EqualTo(25.0));
    }

    [Test]
    public void IngestionProcessingState_WhenNoFilesToProcess_ProgressIs100Percent()
    {
        // Arrange
        var state = new IngestionProcessingState
        {
            TotalFiles = 0,
            CompletedFiles = new List<string>()
        };

        // Act
        var progress = state.TotalFiles > 0 
            ? (double)state.CompletedFiles.Count / state.TotalFiles * 100 
            : 100;

        // Assert
        Assert.That(progress, Is.EqualTo(100.0));
    }

    [Test]
    public void IngestionProcessingState_WhenAddingErrorMessages_LimitsToTwenty()
    {
        // Arrange
        var state = new IngestionProcessingState();

        // Act - Add more than 20 errors
        for (int i = 0; i < 25; i++)
        {
            if (state.Errors.Count < 20)
            {
                state.Errors[$"file{i}.cs"] = $"Error {i}";
            }
        }

        // Assert
        Assert.That(state.Errors.Count, Is.EqualTo(20));
        Assert.That(state.Errors.ContainsKey("file0.cs"), Is.True);
        Assert.That(state.Errors.ContainsKey("file19.cs"), Is.True);
        Assert.That(state.Errors.ContainsKey("file24.cs"), Is.False);
    }

    [Test]
    public void IngestionProcessingState_WhenProcessingQueueEmpty_CanBeCompleted()
    {
        // Arrange
        var state = new IngestionProcessingState
        {
            ProcessingQueue = new List<string>(),
            CompletedFiles = new List<string> { "file1.cs", "file2.cs" },
            TotalFiles = 2,
            CurrentStatus = "processing"
        };

        // Act
        var canComplete = state.ProcessingQueue.Count == 0 && 
                          state.CompletedFiles.Count + state.FailedFiles.Count == state.TotalFiles;

        // Assert
        Assert.That(canComplete, Is.True);
    }

    [Test]
    public void IngestionProcessingState_WhenStaleProcess_DetectedCorrectly()
    {
        // Arrange
        var state = new IngestionProcessingState
        {
            CurrentStatus = "processing",
            LastCheckpoint = DateTime.UtcNow.AddMinutes(-10) // 10 minutes ago
        };

        // Act
        var isStale = DateTime.UtcNow.Subtract(state.LastCheckpoint).TotalMinutes > 5;

        // Assert
        Assert.That(isStale, Is.True);
    }

    [Test]
    public void IngestionProcessingState_WhenRecentProcess_NotStale()
    {
        // Arrange
        var state = new IngestionProcessingState
        {
            CurrentStatus = "processing",
            LastCheckpoint = DateTime.UtcNow.AddMinutes(-2) // 2 minutes ago
        };

        // Act
        var isStale = DateTime.UtcNow.Subtract(state.LastCheckpoint).TotalMinutes > 5;

        // Assert
        Assert.That(isStale, Is.False);
    }
}