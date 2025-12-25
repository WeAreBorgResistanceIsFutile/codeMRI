using System.Text.Json;
using Microsoft.Extensions.Logging;
using codeMRI.MCP.Protocol;

namespace codeMRI.MCP.Server;

/// <summary>
/// A transport-agnostic JSON-RPC server for MCP
/// </summary>
public class JsonRpcServer
{
    private readonly ILogger<JsonRpcServer> _logger;
    private readonly McpProtocolHandler _handler;
    private readonly IMcpTransport _transport;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonRpcServer(
        ILogger<JsonRpcServer> logger, 
        McpProtocolHandler handler,
        IMcpTransport transport)
    {
        _logger = logger;
        _handler = handler;
        _transport = transport;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting JSON-RPC server loop");

        try
        {
            await _transport.StartAsync(async (message) => 
            {
                await HandleMessageInternalAsync(message, cancellationToken);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("JSON-RPC server loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in JSON-RPC server loop");
            throw;
        }
    }

    private async Task HandleMessageInternalAsync(string line, CancellationToken ct)
    {
        _logger.LogInformation("JSON-RPC message received by server: {Message}", line);
        try
        {
            // Parse JSON document to check for ID (request vs notification)
            var jsonDoc = JsonDocument.Parse(line);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("id", out _))
            {
                // It's a notification
                if (root.TryGetProperty("method", out var methodProp))
                {
                    _logger.LogInformation("Received notification: {Method}", methodProp.GetString());
                }
                return;
            }

            // It's a request
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
            if (request != null)
            {
                _logger.LogInformation("Processing JSON-RPC request: {Id} {Method}", request.Id, request.Method);
                var response = await _handler.HandleRequestAsync(request, ct);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                _logger.LogInformation("Sending JSON-RPC response for {Id}", request.Id);
                await _transport.SendMessageAsync(responseJson, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing JSON-RPC message: {Message}", line);
            
            // Attempt to send error response
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
                await _transport.SendMessageAsync(errorJson, ct);
            }
            catch
            {
                // Silently fail if we can't even send an error response
            }
        }
    }
}
