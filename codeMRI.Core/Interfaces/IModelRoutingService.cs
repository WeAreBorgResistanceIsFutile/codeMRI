namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for routing documentation tasks to specialized models based on task type.
/// </summary>
public interface IModelRoutingService
{
    /// <summary>
    ///     Selects the appropriate model for a given documentation task type.
    ///     Returns null to use the default model from settings.
    /// </summary>
    /// <param name="taskType">The type of documentation task</param>
    /// <returns>Model name to use, or null for default</returns>
    string? SelectModelForTask(DocumentationTaskType taskType);
}

/// <summary>
///     Types of documentation generation tasks that may benefit from specialized models.
/// </summary>
public enum DocumentationTaskType
{
    /// <summary>
    ///     Synthesis of multiple outputs into a coherent result.
    ///     Best suited for models with strong reasoning like Kimi, GPT-4.
    /// </summary>
    Synthesis,

    /// <summary>
    ///     Default/unspecified task, uses configured default model.
    /// </summary>
    Default
}