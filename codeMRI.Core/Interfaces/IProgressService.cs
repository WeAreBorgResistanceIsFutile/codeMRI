using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IProgressService
{
    /// <summary>
    /// Reports progress for the current operation.
    /// </summary>
    /// <param name="info">The progress information usually containing local percentage (0-100).</param>
    void Report(ProgressInfo info);

    /// <summary>
    /// Executes an operation within a scaled progress context.
    /// The reported percentages within this scope will be scaled to the defined range (start to start + width).
    /// </summary>
    Task WithScalingAsync(double startPercentage, double widthPercentage, Func<Task> operation);

    /// <summary>
    /// Sets the output handler, usually dealing with the final scaled values.
    /// </summary>
    void SetHandler(Action<ProgressInfo> handler);
}
