using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

public class ASTServiceClient : IASTServiceClient
{
    private const int FailureThreshold = 5;
    private readonly TimeSpan _circuitBreakTimeout = TimeSpan.FromMinutes(1);
    private readonly ICSharpParser _csharpParser;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ASTServiceClient> _logger;
    private readonly ASTServiceSettings _settings;

    // Circuit breaker state
    private volatile bool _circuitOpen;
    private DateTime _circuitOpenTime = DateTime.MinValue;
    private int _failureCount;

    public ASTServiceClient(
        HttpClient httpClient,
        ILogger<ASTServiceClient> logger,
        IOptions<ASTServiceSettings> settings,
        ICSharpParser csharpParser)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        _csharpParser = csharpParser;

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

        for (var attempt = 1; attempt <= maxRetries; attempt++)
            try
            {
                if (!_settings.Enabled)
                    return false;

                var response = await _httpClient.GetAsync("/health", cancellationToken);
                var success = response.IsSuccessStatusCode;

                if (success) _failureCount = 0; // Reset failure count on success
                
                _logger.LogInformation("AST Service health check result: {Success}", success);
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
            var languages = result?.Languages ?? new List<string>();
            
            _logger.LogInformation("AST Service supported languages: {Languages}", string.Join(", ", languages));
            return languages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get supported languages from AST Service");
            return new List<string>();
        }
    }

    public async Task<ASTParseResult?> ParseCodeAsync(string code, string language, string filePath = "",
        CancellationToken cancellationToken = default)
    {
        // 0. Use local Roslyn parser for C#
        if (language.Equals("C#", StringComparison.OrdinalIgnoreCase) || 
            language.Equals("CSharp", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            try 
            {
                return _csharpParser.Parse(code, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse C# code using Roslyn parser locally for {FilePath}", filePath);
                // Optionally fall back to remote or return null?
                // Let's assume remote might handle it differently but user specified Roslyn for C#.
                return null; 
            }
        }

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

        for (var attempt = 1; attempt <= maxRetries; attempt++)
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
                _logger.LogInformation("Sending AST Parse request for {FilePath} ({Language}). Code length: {Length}", filePath, language, code.Length);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/ast/parse", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var rawResult = JsonSerializer.Deserialize<RawDependencyData>(responseContent, _jsonOptions);

                // Convert to Core ASTParseResult
                if (rawResult == null)
                    return null;

                var result = new ASTParseResult
                {
                    Language = rawResult.Language,
                    FilePath = rawResult.FilePath,
                    Tree = rawResult.Tree,
                    Timestamp = rawResult.Timestamp,
                    Metrics = rawResult.Metrics,
                    DependencyGraph = rawResult.DependencyGraph,
                    EntryPoints = rawResult.EntryPoints,
                    HierarchicalStructure = rawResult.HierarchicalStructure,
                    CrossModuleReferences = rawResult.CrossModuleReferences
                };

                RecordSuccess();
                return result;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "AST Service request failed on attempt {Attempt}/{MaxRetries} for {FilePath}. Retrying...",
                    attempt, maxRetries, filePath);

                // Exponential backoff with jitter
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1) +
                                                      Random.Shared.Next(0, 500));
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "AST Service request timed out on attempt {Attempt}/{MaxRetries} for {FilePath}. Retrying...",
                    attempt, maxRetries, filePath);

                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                RecordFailure();
                _logger.LogError(ex,
                    "Failed to parse code using AST Service for language: {Language}, file: {FilePath} after {Attempts} attempts",
                    language, filePath, attempt);
                return null;
            }

        RecordFailure();
        return null;
    }

    public Task<List<CodeComponent>> ConvertToCodeComponentsAsync(ASTParseResult astResult,
        CancellationToken cancellationToken = default)
    {
        var components = new List<CodeComponent>();

        if (astResult?.HierarchicalStructure == null)
            return Task.FromResult(components);

        var fileName = Path.GetFileNameWithoutExtension(astResult.FilePath);
        var componentCounter = 1;

        try
        {
            // Handle JsonElement case (most common when deserialized via System.Text.Json)
            if (astResult.HierarchicalStructure is JsonElement rootElement &&
                rootElement.ValueKind == JsonValueKind.Object)
            {
                // Try to extract classes
                if (TryGetPropertyCaseInsensitive(rootElement, "Classes", out var classesElement) &&
                    classesElement.ValueKind == JsonValueKind.Array)
                    foreach (var classInfo in classesElement.EnumerateArray())
                    {
                        var component = new CodeComponent
                        {
                            Id = $"{fileName}_{GetJsonString(classInfo, "Name")}_{componentCounter++}",
                            Name = GetJsonString(classInfo, "Name"),
                            Type = DetermineComponentTypeJson(classInfo, astResult.Language),
                            FilePath = astResult.FilePath,
                            Language = astResult.Language,
                            LineCount = GetJsonMetrics(classInfo, "Lines"),
                            ComplexityScore = GetJsonMetrics(classInfo, "Complexity"),
                            Methods = GetJsonList(classInfo, "Methods"),
                            Properties = GetJsonList(classInfo, "Properties"),
                            Dependencies = ExtractDependenciesDynamic(astResult),
                            Description = GenerateDescriptionJson(classInfo, astResult.Language)
                        };

                        components.Add(component);
                    }

                // Try to extract standalone functions
                if (TryGetPropertyCaseInsensitive(rootElement, "Functions", out var functionsElement) &&
                    functionsElement.ValueKind == JsonValueKind.Array)
                    foreach (var functionInfo in functionsElement.EnumerateArray())
                        // Only include standalone functions (Parent is null)
                        if (!TryGetPropertyCaseInsensitive(functionInfo, "Parent", out var parent) ||
                            parent.ValueKind == JsonValueKind.Null)
                        {
                            var component = new CodeComponent
                            {
                                Id = $"{fileName}_{GetJsonString(functionInfo, "Name")}_{componentCounter++}",
                                Name = GetJsonString(functionInfo, "Name"),
                                Type = "Function",
                                FilePath = astResult.FilePath,
                                Language = astResult.Language,
                                LineCount = GetJsonMetrics(functionInfo, "Lines"),
                                ComplexityScore = GetJsonMetrics(functionInfo, "Complexity"),
                                Methods = new List<string>(),
                                Properties = new List<string>(),
                                Dependencies = ExtractDependenciesDynamic(astResult),
                                Description = GenerateFunctionDescriptionJson(functionInfo, astResult.Language)
                            };

                            components.Add(component);
                        }

                // Fallback: If no components found but file has content, treat as Script/Module
                if (components.Count == 0 && astResult.Metrics is JsonElement metricsElement)
                {
                    int lines = 0;
                    if (TryGetPropertyCaseInsensitive(metricsElement, "linesOfCode", out var linesProp) && linesProp.ValueKind == JsonValueKind.Number)
                         lines = linesProp.GetInt32();
                    else if (TryGetPropertyCaseInsensitive(metricsElement, "lines", out var linesProp2) && linesProp2.ValueKind == JsonValueKind.Number)
                         lines = linesProp2.GetInt32();

                    if (lines > 0)
                    {
                        int complexity = 1;
                        if (TryGetPropertyCaseInsensitive(metricsElement, "cyclomaticComplexity", out var compProp) && compProp.ValueKind == JsonValueKind.Number)
                             complexity = compProp.GetInt32();
                        else if (TryGetPropertyCaseInsensitive(metricsElement, "complexity", out var compProp2) && compProp2.ValueKind == JsonValueKind.Number)
                             complexity = compProp2.GetInt32();

                        components.Add(new CodeComponent
                        {
                            Id = $"{fileName}_script",
                            Name = fileName,
                            Type = "Script",
                            FilePath = astResult.FilePath,
                            Language = astResult.Language,
                            LineCount = lines,
                            ComplexityScore = complexity > 0 ? complexity : 1,
                            Methods = new List<string>(),
                            Properties = new List<string>(),
                            Dependencies = ExtractDependenciesDynamic(astResult),
                            Description = $"Script {fileName} in {astResult.Language} with {lines} lines of code"
                        });
                    }
                }

                return Task.FromResult(components);
            }

            // Legacy fallback for dynamic objects (e.g. tests)
            dynamic hierarchicalStructure = astResult.HierarchicalStructure;

            // Try to extract classes
            if (hierarchicalStructure.Classes != null)
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

            // Try to extract standalone functions
            if (hierarchicalStructure.Functions != null)
                foreach (var functionInfo in ((IEnumerable<dynamic>)hierarchicalStructure.Functions).Where(f =>
                             f.Parent == null))
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
                
            // Fallback for dynamic: If no components found
            if (components.Count == 0)
            {
                 dynamic metrics = astResult.Metrics;
                 if (metrics != null)
                 {
                     int lines = 0;
                     try { lines = metrics.linesOfCode; } catch {}
                     if (lines == 0) try { lines = metrics.Lines; } catch {}

                     if (lines > 0)
                     {
                         int complexity = 1;
                         try { complexity = metrics.cyclomaticComplexity; } catch {}
                         if (complexity <= 0) try { complexity = metrics.Complexity; } catch {}

                         components.Add(new CodeComponent
                         {
                             Id = $"{fileName}_script",
                             Name = fileName,
                             Type = "Script",
                             FilePath = astResult.FilePath,
                             Language = astResult.Language,
                             LineCount = lines,
                             ComplexityScore = complexity,
                             Methods = new List<string>(),
                             Properties = new List<string>(),
                             Dependencies = ExtractDependenciesDynamic(astResult),
                             Description = $"Script {fileName} in {astResult.Language} with {lines} lines of code"
                         });
                     }
                 }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert AST result to components");
        }

        return Task.FromResult(components);
    }

    private bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        // 1. Try exact match
        if (element.TryGetProperty(propertyName, out value))
            return true;

        // 2. Try camelCase (e.g., "Lines" -> "lines")
        var camelCase = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        if (element.TryGetProperty(camelCase, out value))
            return true;

        // 3. Try lowercase (e.g., "Classes" -> "classes")
        if (element.TryGetProperty(propertyName.ToLowerInvariant(), out value))
            return true;

        value = default;
        return false;
    }

    private string GetJsonString(JsonElement element, string propertyName)
    {
        return TryGetPropertyCaseInsensitive(element, propertyName, out var prop) ? prop.ToString() : string.Empty;
    }

    private int GetJsonMetrics(JsonElement element, string metricName)
    {
        if (TryGetPropertyCaseInsensitive(element, "Metrics", out var metrics) &&
            TryGetPropertyCaseInsensitive(metrics, metricName, out var val) &&
            val.TryGetInt32(out var result))
            return result;
        return metricName == "Complexity" ? 1 : 0;
    }

    private List<string> GetJsonList(JsonElement element, string listName)
    {
        var result = new List<string>();
        if (TryGetPropertyCaseInsensitive(element, listName, out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray())
                result.Add(item.ToString());
        return result;
    }

    private string DetermineComponentTypeJson(JsonElement classInfo, string language)
    {
        var name = GetJsonString(classInfo, "Name");
        var type = GetJsonString(classInfo, "Type");

        if (name.ToLowerInvariant().Contains("controller")) return "Controller";
        if (name.ToLowerInvariant().Contains("service")) return "Service";
        if (name.ToLowerInvariant().Contains("repository")) return "Repository";

        return !string.IsNullOrEmpty(type) ? type : "Class";
    }

    private string GenerateDescriptionJson(JsonElement classInfo, string language)
    {
        var name = GetJsonString(classInfo, "Name");
        if (string.IsNullOrEmpty(name)) name = "Unknown";

        var lines = GetJsonMetrics(classInfo, "Lines");
        var complexity = GetJsonMetrics(classInfo, "Complexity");
        var type = GetJsonString(classInfo, "Type");
        if (string.IsNullOrEmpty(type)) type = "Class";

        return $"{type} {name} in {language} with {lines} lines of code and complexity score of {complexity}";
    }

    private string GenerateFunctionDescriptionJson(JsonElement functionInfo, string language)
    {
        var name = GetJsonString(functionInfo, "Name");
        if (string.IsNullOrEmpty(name)) name = "Unknown";

        var lines = GetJsonMetrics(functionInfo, "Lines");
        var complexity = GetJsonMetrics(functionInfo, "Complexity");

        return $"Function {name} in {language} with {lines} lines of code and complexity score of {complexity}";
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
                // Check if it's the typed DependencyGraphData
                if (astResult.DependencyGraph is DependencyGraphData graphData)
                    return graphData.Dependencies ?? new List<string>();

                // Fallback: Try to access Dependencies property dynamically
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