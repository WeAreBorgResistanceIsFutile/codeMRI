using System.Text.Json.Serialization;

namespace codeMRI.Core.Models;

/// <summary>
///     Tracks the state of an ongoing ingestion process for resumable operation
/// </summary>
public class IngestionProcessingState
{
    /// <summary>
    ///     Files that need to be processed (queue)
    /// </summary>
    [JsonPropertyName("processingQueue")]
    public List<string> ProcessingQueue { get; set; } = new();

    /// <summary>
    ///     Files that have been successfully processed
    /// </summary>
    [JsonPropertyName("completedFiles")]
    public List<string> CompletedFiles { get; set; } = new();

    /// <summary>
    ///     Files that failed to process
    /// </summary>
    [JsonPropertyName("failedFiles")]
    public List<string> FailedFiles { get; set; } = new();

    /// <summary>
    ///     Current batch size for processing
    /// </summary>
    [JsonPropertyName("currentBatchSize")]
    public int CurrentBatchSize { get; set; } = 50;

    /// <summary>
    ///     Timestamp of the last checkpoint
    /// </summary>
    [JsonPropertyName("lastCheckpoint")]
    public DateTime LastCheckpoint { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Current status of the ingestion process
    /// </summary>
    [JsonPropertyName("currentStatus")]
    public string CurrentStatus { get; set; } = "idle";

    /// <summary>
    ///     Current position in the processing queue
    /// </summary>
    [JsonPropertyName("currentPosition")]
    public int CurrentPosition { get; set; } = 0;

    /// <summary>
    ///     Total number of files to process
    /// </summary>
    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; } = 0;

    /// <summary>
    ///     Timestamp when processing started
    /// </summary>
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     Timestamp when processing completed
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Error messages from failed processing attempts
    /// </summary>
    [JsonPropertyName("errors")]
    public Dictionary<string, string> Errors { get; set; } = new();
    
    /// <summary>
    ///     IDs of modules that have been successfully documented
    /// </summary>
    [JsonPropertyName("completedModuleIds")]
    public HashSet<string> CompletedModuleIds { get; set; } = new();

    /// <summary>
    ///     Serialized version of the unified dependency and hierarchy graph
    /// </summary>
    [JsonPropertyName("serializedGraph")]
    public string? SerializedGraph { get; set; }

    /// <summary>
    ///     Serialized version of the rubric used for evaluation
    /// </summary>
    [JsonPropertyName("serializedRubric")]
    public string? SerializedRubric { get; set; }
}