using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using codeMRI.MCP.Protocol;

namespace codeMRI.MCP.Server;

public class JsonRpcServer
{
    private readonly ILogger<JsonRpcServer> _logger;
    private readonly McpProtocolHandler _handler;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonRpcServer(ILogger<JsonRpcServer> logger, McpProtocolHandler handler)
    {
        _logger = logger;
        _handler = handler;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP Server started, listening on stdin/stdout");

        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            using var writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                
                if (line == null)
                {
                    _logger.LogInformation("Stdin closed, shutting down");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    // First, check if this is a notification (no id field)
                    var jsonDoc = JsonDocument.Parse(line);
                    var root = jsonDoc.RootElement;

                    if (!root.TryGetProperty("id", out _))
                    {
                        // It's a notification - just log and skip
                        if (root.TryGetProperty("method", out var methodProp))
                        {
                            _logger.LogDebug("Received notification: {Method}", methodProp.GetString());
                        }
                        continue;
                    }

                    // It's a request - deserialize and handle
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                    if (request != null)
                    {
                        var response = await _handler.HandleRequestAsync(request, cancellationToken);
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing JSON-RPC message: {Message}", line);
                    
                    // Send error response if we can parse the ID
                    try
                    {
                        var errorDoc = JsonDocument.Parse(line);
                        var id = errorDoc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        
                        var errorResponse = new JsonRpcResponse
                        {
                            Id = id,
                            Error = new JsonRpcError
                            {
                                Code = -32603,
                                Message = "Internal error",
                                Data = ex.Message
                            }
                        };
                        
                        var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                        await writer.WriteLineAsync(errorJson);
                    }
                    catch
                    {
                        // Can't send error response, skip
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in JSON-RPC server");
            throw;
        }
    }
}
