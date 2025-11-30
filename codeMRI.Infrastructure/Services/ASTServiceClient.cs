using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Shared.Models;
using codeMRI.Core.Interfaces;

namespace codeMRI.Infrastructure.Services;

public class ASTServiceClient : IASTServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ASTServiceClient> _logger;
    private readonly ASTServiceSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // Circuit breaker state
    private volatile bool _circuitOpen = false;
    private DateTime _circuitOpenTime = DateTime.MinValue;
    private readonly TimeSpan _circuitBreakTimeout = TimeSpan.FromMinutes(1);
    private int _failureCount = 0;
    private const int FailureThreshold = 5;

    public ASTServiceClient(
        HttpClient httpClient,
        ILogger<ASTServiceClient> logger,
        IOptions<ASTServiceSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (_circuitOpen)
        {
            if (DateTime.UtcNow - _circuitOpenTime > _circuitBreakTimeout)
            {
                _circuitOpen = false;
                _failureCount = 0;
                _logger.LogInformation("Circuit breaker reset for AST Service");
            }
            else
            {
                return false;
            }
        }
        
        const int maxRetries = 2;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!_settings.Enabled)
                    return false;

                var response = await _httpClient.GetAsync("/health", cancellationToken);
                var success = response.IsSuccessStatusCode;
                
                if (success)
                {
                    _failureCount = 0; // Reset failure count on success
                }
                
                return success;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogDebug(ex, "AST Service health check failed on attempt {Attempt}/{MaxRetries}. Retrying...", 
                    attempt, maxRetries);
                
                await Task.Delay(500 * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                RecordFailure();
                _logger.LogWarning(ex, "AST Service health check failed after {Attempts} attempts", attempt);
                return false;
            }
        }
        
        RecordFailure();
        return false;
    }

    public async Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_settings.Enabled)
                return new List<string>();

            var response = await _httpClient.GetAsync("/api/ast/supported-languages", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SupportedLanguagesResponse>(content, _jsonOptions);
            
            return result?.Languages ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get supported languages from AST Service");
            return new List<string>();
        }
    }

    public async Task<ASTParseResult?> ParseCodeAsync(string code, string language, string filePath = "", CancellationToken cancellationToken = default)
    {
        if (_circuitOpen)
        {
            if (DateTime.UtcNow - _circuitOpenTime > _circuitBreakTimeout)
            {
                _circuitOpen = false;
                _failureCount = 0;
                _logger.LogInformation("Circuit breaker reset for AST Service");
            }
            else
            {
                _logger.LogDebug("Circuit breaker is open, skipping AST Service call for {FilePath}", filePath);
                return null;
            }
        }
        
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!_settings.Enabled)
                    return null;

                var request = new ParseRequest
                {
                    Code = code,
                    Language = language,
                    FilePath = filePath
                };

                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("/api/ast/parse", content, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var internalResult = JsonSerializer.Deserialize<InternalASTParseResult>(responseContent, _jsonOptions);
                
                // Convert to Core ASTParseResult
                if (internalResult == null)
                    return null;
                
                var result = new ASTParseResult
                {
                    Language = internalResult.Language,
                    FilePath = internalResult.FilePath,
                    Tree = internalResult.Tree,
                    Timestamp = internalResult.Timestamp,
                    Metrics = internalResult.Metrics,
                    DependencyGraph = internalResult.DependencyGraph,
                    EntryPoints = internalResult.EntryPoints.Cast<object>().ToList(),
                    HierarchicalStructure = internalResult.HierarchicalStructure,
                    CrossModuleReferences = internalResult.CrossModuleReferences.Cast<object>().ToList()
                };
                
                RecordSuccess();
                return result;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "AST Service request failed on attempt {Attempt}/{MaxRetries} for {FilePath}. Retrying...", 
                    attempt, maxRetries, filePath);
                
                // Exponential backoff with jitter
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 500));
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "AST Service request timed out on attempt {Attempt}/{MaxRetries} for {FilePath}. Retrying...", 
                    attempt, maxRetries, filePath);
                
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                RecordFailure();
                _logger.LogError(ex, "Failed to parse code using AST Service for language: {Language}, file: {FilePath} after {Attempts} attempts", 
                    language, filePath, attempt);
                return null;
            }
        }
        
        RecordFailure();
        return null;
    }

    public Task<List<CodeComponent>> ConvertToCodeComponentsAsync(ASTParseResult astResult, CancellationToken cancellationToken = default)
    {
        var components = new List<CodeComponent>();
        
        if (astResult?.HierarchicalStructure == null)
            return Task.FromResult(components);

        var fileName = Path.GetFileNameWithoutExtension(astResult.FilePath);
        var componentCounter = 1;

        // Use dynamic to access the object properties since we don't have concrete types
        dynamic hierarchicalStructure = astResult.HierarchicalStructure;
        
        try
        {
            // Try to extract classes
            if (hierarchicalStructure.Classes != null)
            {
                foreach (var classInfo in (IEnumerable<dynamic>)hierarchicalStructure.Classes)
                {
                    var component = new CodeComponent
                    {
                        Id = $"{fileName}_{classInfo.Name}_{componentCounter++}",
                        Name = classInfo.Name,
                        Type = DetermineComponentType(classInfo, astResult.Language),
                        FilePath = astResult.FilePath,
                        Language = astResult.Language,
                        LineCount = classInfo.Metrics?.Lines ?? 0,
                        ComplexityScore = classInfo.Metrics?.Complexity ?? 1,
                        Methods = ExtractMethodsDynamic(classInfo),
                        Properties = ExtractPropertiesDynamic(classInfo),
                        Dependencies = ExtractDependenciesDynamic(astResult),
                        Description = GenerateDescriptionDynamic(classInfo, astResult.Language)
                    };

                    components.Add(component);
                }
            }

            // Try to extract standalone functions
            if (hierarchicalStructure.Functions != null)
            {
                foreach (var functionInfo in ((IEnumerable<dynamic>)hierarchicalStructure.Functions).Where(f => f.Parent == null))
                {
                    var component = new CodeComponent
                    {
                        Id = $"{fileName}_{functionInfo.Name}_{componentCounter++}",
                        Name = functionInfo.Name,
                        Type = "Function",
                        FilePath = astResult.FilePath,
                        Language = astResult.Language,
                        LineCount = functionInfo.Metrics?.Lines ?? 0,
                        ComplexityScore = functionInfo.Metrics?.Complexity ?? 1,
                        Methods = new List<string>(),
                        Properties = new List<string>(),
                        Dependencies = ExtractDependenciesDynamic(astResult),
                        Description = GenerateFunctionDescriptionDynamic(functionInfo, astResult.Language)
                    };

                    components.Add(component);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert AST result to components");
        }

        return Task.FromResult(components);
    }

    private List<string> ExtractMethodsDynamic(dynamic classInfo)
    {
        try
        {
            if (classInfo?.Methods != null)
            {
                var methods = classInfo.Methods as List<object>;
                if (methods != null)
                    return methods.Select(m => m?.ToString() ?? "").ToList();
            }
            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private List<string> ExtractPropertiesDynamic(dynamic classInfo)
    {
        try
        {
            if (classInfo?.Properties != null)
            {
                var properties = classInfo.Properties as List<object>;
                if (properties != null)
                    return properties.Select(p => p?.ToString() ?? "").ToList();
            }
            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private List<string> ExtractDependenciesDynamic(ASTParseResult astResult)
    {
        try
        {
            if (astResult?.DependencyGraph != null)
            {
                // Try to access Dependencies property dynamically
                var dependencyGraph = astResult.DependencyGraph as dynamic;
                if (dependencyGraph?.Dependencies != null)
                {
                    var deps = dependencyGraph.Dependencies as List<object>;
                    if (deps != null)
                        return deps.Select(d => d?.ToString() ?? "").ToList();
                }
            }
            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private string GenerateDescriptionDynamic(dynamic classInfo, string language)
    {
        try
        {
            var name = classInfo.Name?.ToString() ?? "Unknown";
            var lines = classInfo.Metrics?.Lines ?? 0;
            var complexity = classInfo.Metrics?.Complexity ?? 1;
            
            var description = $"{classInfo.Type} {name} in {language}";
            description += $" with {lines} lines of code";
            description += $" and complexity score of {complexity}";

            return description;
        }
        catch
        {
            return $"Component in {language}";
        }
    }

    private string GenerateFunctionDescriptionDynamic(dynamic functionInfo, string language)
    {
        try
        {
            var name = functionInfo.Name?.ToString() ?? "Unknown";
            var lines = functionInfo.Metrics?.Lines ?? 0;
            var complexity = functionInfo.Metrics?.Complexity ?? 1;
            
            var description = $"Function {name} in {language}";
            description += $" with {lines} lines of code";
            description += $" and complexity score of {complexity}";

            return description;
        }
        catch
        {
            return $"Function in {language}";
        }
    }

    private string DetermineComponentType(dynamic classInfo, string language)
    {
        try
        {
            var name = classInfo.Name?.ToString() ?? "";
            
            // Check for common patterns based on naming and attributes
            if (name.ToLowerInvariant().Contains("controller"))
                return "Controller";
            
            if (name.ToLowerInvariant().Contains("service"))
                return "Service";
            
            if (name.ToLowerInvariant().Contains("repository"))
                return "Repository";

            return classInfo.Type?.ToString() ?? "Class";
        }
        catch
        {
            return "Class";
        }
    }
    
    private void RecordFailure()
    {
        _failureCount++;
        if (_failureCount >= FailureThreshold)
        {
            _circuitOpen = true;
            _circuitOpenTime = DateTime.UtcNow;
            _logger.LogWarning("Circuit breaker opened for AST Service after {FailureCount} failures", _failureCount);
        }
    }
    
    private void RecordSuccess()
    {
        _failureCount = 0;
        if (_circuitOpen)
        {
            _circuitOpen = false;
            _logger.LogInformation("Circuit breaker closed for AST Service after successful operation");
        }
    }
}

// DTOs for AST Service communication
public class ParseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class SupportedLanguagesResponse
{
    public List<string> Languages { get; set; } = new();
}

// Internal DTO for deserialization from AST Service
public class InternalASTParseResult
{
    public string Language { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public object? Tree { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public object Metrics { get; set; } = new();
    public object DependencyGraph { get; set; } = new();
    public List<object> EntryPoints { get; set; } = new();
    public object HierarchicalStructure { get; set; } = new();
    public List<object> CrossModuleReferences { get; set; } = new();
}