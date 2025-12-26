namespace codeMRI.Core.Interfaces;

/// <summary>
///     Service for initializing vector store collections at application startup.
/// </summary>
public interface IVectorStoreInitializationService
{
    /// <summary>
    ///     Ensures all required vector store collections exist.
    ///     Creates any missing collections with appropriate configuration.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
