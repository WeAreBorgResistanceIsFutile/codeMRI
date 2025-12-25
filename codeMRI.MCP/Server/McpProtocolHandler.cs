using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using codeMRI.MCP.Protocol;

namespace codeMRI.MCP.Server;

public class McpProtocolHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpProtocolHandler> _logger;
    private readonly Dictionary<string, ToolMetadata> _tools = new();

    public McpProtocolHandler(IServiceProvider serviceProvider, ILogger<McpProtocolHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void DiscoverTools()
    {
        _logger.LogInformation("Discovering tools...");
        
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var toolAttr = method.GetCustomAttribute<McpToolAttribute>();
                if (toolAttr != null)
                {
                    var toolName = toolAttr.Name ?? method.Name;
                    var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    
                    var metadata = new ToolMetadata
                    {
                        Name = toolName,
                        Description = description,
                        Method = method,
                        DeclaringType = type
                    };

                    // Build input schema from parameters
                    var parameters = method.GetParameters();
                    foreach (var param in parameters)
                    {
                        var paramDesc = param.GetCustomAttribute<DescriptionAttribute>()?.Description;
                        metadata.InputSchema.Properties[param.Name!] = new PropertySchema
                        {
                            Type = GetJsonType(param.ParameterType),
                            Description = paramDesc
                        };
                        
                        if (!param.IsOptional && !param.HasDefaultValue)
                        {
                            metadata.InputSchema.Required ??= new List<string>();
                            metadata.InputSchema.Required.Add(param.Name!);
                        }
                    }

                    _tools[toolName] = metadata;
                    _logger.LogInformation("Discovered tool: {ToolName} ({Description})", toolName, description);
                }
            }
        }
        
        _logger.LogInformation("Tool discovery complete. Found {Count} tools", _tools.Count);
    }

    private string GetJsonType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))) return "array";
        return "object";
    }

    public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling request: {Method}", request.Method);

        try
        {
            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCallAsync(request, cancellationToken),
                _ => throw new Exception($"Unknown method: {request.Method}")
            };

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request {Method}", request.Method);
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = ex.Message
                }
            };
        }
    }

    private InitializeResult HandleInitialize(JsonRpcRequest request)
    {
        // Discover tools on first initialize
        if (_tools.Count == 0)
        {
            DiscoverTools();
        }

        return new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            ServerInfo = new Implementation
            {
                Name = "codeMRI.MCP",
                Version = "1.0.0"
            },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability(),
                Logging = new { }
            }
        };
    }

    private ListToolsResult HandleToolsList()
    {
        var tools = _tools.Values.Select(t => new Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).ToList();

        _logger.LogInformation("Returning {Count} tools", tools.Count);
        foreach (var tool in tools)
        {
            _logger.LogInformation("Tool: {Name} - {Description}", tool.Name, tool.Description);
        }

        return new ListToolsResult { Tools = tools };
    }

    private async Task<CallToolResult> HandleToolsCallAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Params == null)
            throw new Exception("Missing params");

        var callParams = JsonSerializer.Deserialize<CallToolParams>(request.Params.Value.GetRawText());
        if (callParams == null || string.IsNullOrEmpty(callParams.Name))
            throw new Exception("Invalid tool call params");

        if (!_tools.TryGetValue(callParams.Name, out var tool))
            throw new Exception($"Tool not found: {callParams.Name}");

        try
        {
            // Get or create instance of the tool class
            var instance = ActivatorUtilities.CreateInstance(_serviceProvider, tool.DeclaringType);

            // Prepare parameters
            var methodParams = tool.Method.GetParameters();
            var args = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                if (callParams.Arguments != null && callParams.Arguments.TryGetValue(param.Name!, out var value))
                {
                    // Convert JSON value to parameter type
                    args[i] = ConvertParameter(value, param.ParameterType);
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else
                {
                    args[i] = null;
                }
            }

            // Invoke the method
            var resultTask = tool.Method.Invoke(instance, args);
            
            // Handle async methods
            string resultText;
            if (resultTask is Task task)
            {
                await task;
                var resultProperty = task.GetType().GetProperty("Result");
                var result = resultProperty?.GetValue(task);
                resultText = result?.ToString() ?? "";
            }
            else
            {
                resultText = resultTask?.ToString() ?? "";
            }

            return new CallToolResult
            {
                Content = new List<Content>
                {
                    new Content { Type = "text", Text = resultText }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", callParams.Name);
            return new CallToolResult
            {
                Content = new List<Content>
                {
                    new Content { Type = "text", Text = $"Error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    private object? ConvertParameter(object? value, Type targetType)
    {
        if (value == null) return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.Deserialize(targetType);
        }

        return Convert.ChangeType(value, targetType);
    }
}

public class ToolMetadata
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public MethodInfo Method { get; set; } = null!;
    public Type DeclaringType { get; set; } = null!;
    public ToolInputSchema InputSchema { get; set; } = new();
}
