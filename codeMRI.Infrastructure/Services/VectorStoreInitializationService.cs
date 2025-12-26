using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

public class VectorStoreInitializationService : IVectorStoreInitializationService
{
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IEmbeddingService _embeddingService;
    private readonly VectorStoreSettings _settings;
    private readonly ILogger<VectorStoreInitializationService> _logger;

    // Collection names and their purposes
    private readonly Dictionary<string, string> _requiredCollections = new()
    {
        { "documentation", "Stores documentation page embeddings" },
        { "code", "Stores code chunk embeddings" }
    };

    public VectorStoreInitializationService(
        IVectorStoreService vectorStoreService,
        IEmbeddingService embeddingService,
        IOptions<VectorStoreSettings> settings,
        ILogger<VectorStoreInitializationService> logger)
    {
        _vectorStoreService = vectorStoreService;
        _embeddingService = embeddingService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing vector store collections...");
        
        var vectorSize = _embeddingService.GetDimensions();
        var created = 0;
        var existing = 0;

        foreach (var (collectionName, purpose) in _requiredCollections)
        {
            try
            {
                await _vectorStoreService.CreateCollectionAsync(collectionName, vectorSize);
                created++;
                _logger.LogInformation("Initialized collection '{Collection}': {Purpose}", 
                    collectionName, purpose);
            }
            catch (Exception ex)
            {
                // CreateCollectionAsync already logs if collection exists, 
                // but we catch any other errors here
                _logger.LogError(ex, "Failed to initialize collection '{Collection}'", collectionName);
            }
        }

        _logger.LogInformation(
            "Vector store initialization complete. Created: {Created}, Existing: {Existing}", 
            created, existing);
    }
}
