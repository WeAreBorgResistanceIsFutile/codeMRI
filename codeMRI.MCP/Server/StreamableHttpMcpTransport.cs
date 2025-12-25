using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using codeMRI.MCP.Protocol;

namespace codeMRI.MCP.Server;

/// <summary>
/// MCP transport implementing the Streamable HTTP protocol (MCP 2025-03-26)
/// Single endpoint supporting POST for messages and GET for SSE streaming.
/// </summary>
public class StreamableHttpMcpTransport : IMcpTransport
{
    private readonly ILogger<StreamableHttpMcpTransport> _logger;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, StreamableSession> _sessions = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private Func<string, Task>? _onMessageReceived;
    
    // For coordinating request-response pairs
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses = new();

    public StreamableHttpMcpTransport(ILogger<StreamableHttpMcpTransport> logger, int port = 8080)
    {
        _logger = logger;
        _port = port;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task StartAsync(Func<string, Task> onMessageReceived, CancellationToken ct)
    {
        _onMessageReceived = onMessageReceived;

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();

        // Single MCP endpoint - the core of Streamable HTTP
        app.MapPost("/mcp", HandlePostAsync);
        app.MapGet("/mcp", HandleGetAsync);
        app.MapDelete("/mcp", HandleDeleteAsync);
        
        // Backward compatibility endpoints (deprecated SSE protocol)
        app.MapGet("/sse", HandleLegacySseAsync);
        app.MapPost("/message", HandleLegacyMessageAsync);

        _logger.LogInformation("Streamable HTTP transport listening on port {Port}", _port);
        _logger.LogInformation("Endpoints: POST/GET/DELETE /mcp (Streamable HTTP), GET /sse + POST /message (legacy)");

        await app.RunAsync($"http://0.0.0.0:{_port}");
    }

    public async Task SendMessageAsync(string message, CancellationToken ct)
    {
        // Parse the message to find the request ID
        try
        {
            var doc = JsonDocument.Parse(message);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                var id = idProp.ToString();
                if (_pendingResponses.TryRemove(id, out var tcs))
                {
                    tcs.TrySetResult(message);
                    return;
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, fall through
        }

        // Broadcast to all SSE sessions (for server-initiated messages)
        foreach (var session in _sessions.Values)
        {
            session.MessageQueue.Enqueue(message);
            session.Signal.Set();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// POST /mcp - Receives JSON-RPC messages from client
    /// Returns either JSON or SSE stream based on message type
    /// </summary>
    private async Task HandlePostAsync(HttpContext context)
    {
        // Validate Accept header
        var accept = context.Request.Headers.Accept.ToString();
        if (!accept.Contains("application/json") && !accept.Contains("text/event-stream") && !string.IsNullOrEmpty(accept) && accept != "*/*")
        {
            context.Response.StatusCode = 406; // Not Acceptable
            return;
        }

        // Get or create session
        var sessionId = context.Request.Headers["Mcp-Session-Id"].ToString();
        StreamableSession? session = null;
        
        if (!string.IsNullOrEmpty(sessionId))
        {
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                // Session not found - client should re-initialize
                context.Response.StatusCode = 404;
                return;
            }
        }

        // Read the request body
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        
        _logger.LogInformation("POST /mcp received: {Body}", body.Length > 200 ? body[..200] + "..." : body);

        try
        {
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Check if it's a request (has id) or notification/response (no id or is response)
            bool isRequest = root.TryGetProperty("id", out var idProp) && 
                           root.TryGetProperty("method", out _);
            
            if (isRequest)
            {
                var requestId = idProp.ToString();
                
                // Handle initialize request - create new session
                if (root.TryGetProperty("method", out var methodProp) && 
                    methodProp.GetString() == "initialize")
                {
                    sessionId = Guid.NewGuid().ToString();
                    session = new StreamableSession { SessionId = sessionId };
                    _sessions.TryAdd(sessionId, session);
                    _logger.LogInformation("Created new session: {SessionId}", sessionId);
                }

                // Create a TaskCompletionSource for this request's response
                var tcs = new TaskCompletionSource<string>();
                _pendingResponses.TryAdd(requestId, tcs);

                // Process the message through the handler
                if (_onMessageReceived != null)
                {
                    await _onMessageReceived(body);
                }

                // Wait for the response (with timeout)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    var response = await tcs.Task.WaitAsync(cts.Token);
                    
                    // Return JSON response
                    context.Response.ContentType = "application/json";
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        context.Response.Headers["Mcp-Session-Id"] = sessionId;
                    }
                    await context.Response.WriteAsync(response);
                }
                catch (OperationCanceledException)
                {
                    _pendingResponses.TryRemove(requestId, out _);
                    context.Response.StatusCode = 504; // Gateway Timeout
                }
            }
            else
            {
                // It's a notification or response - just acknowledge
                if (_onMessageReceived != null)
                {
                    await _onMessageReceived(body);
                }
                context.Response.StatusCode = 202; // Accepted
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in POST /mcp");
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                error = new { code = -32700, message = "Parse error" }
            }, _jsonOptions));
        }
    }

    /// <summary>
    /// GET /mcp - Opens an SSE stream for server-to-client messages
    /// Supports both session-based and standalone SSE streams per MCP 2025-03-26
    /// </summary>
    private async Task HandleGetAsync(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        if (!accept.Contains("text/event-stream") && !string.IsNullOrEmpty(accept) && accept != "*/*")
        {
            context.Response.StatusCode = 405; // Method Not Allowed
            return;
        }

        var sessionId = context.Request.Headers["Mcp-Session-Id"].ToString();
        StreamableSession? session = null;

        if (!string.IsNullOrEmpty(sessionId))
        {
            // Existing session
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                context.Response.StatusCode = 404; // Session not found
                return;
            }
        }
        else
        {
            // Standalone SSE stream - create ephemeral session
            sessionId = Guid.NewGuid().ToString();
            session = new StreamableSession { SessionId = sessionId };
            _sessions.TryAdd(sessionId, session);
            _logger.LogInformation("Created standalone SSE session: {SessionId}", sessionId);
        }

        _logger.LogInformation("GET /mcp SSE stream opened for session: {SessionId}", sessionId);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["Mcp-Session-Id"] = sessionId;
        await context.Response.Body.FlushAsync();

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                if (session.MessageQueue.TryDequeue(out var message))
                {
                    var sseData = $"event: message\ndata: {message}\n\n";
                    await context.Response.WriteAsync(sseData);
                    await context.Response.Body.FlushAsync();
                }
                else
                {
                    var signaled = await Task.Run(() => session.Signal.WaitOne(15000), context.RequestAborted);
                    if (!signaled)
                    {
                        // Send heartbeat
                        await context.Response.WriteAsync(": heartbeat\n\n");
                        await context.Response.Body.FlushAsync();
                    }
                }
            }
        }
        finally
        {
            _logger.LogInformation("GET /mcp SSE stream closed for session: {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// DELETE /mcp - Terminates a session
    /// </summary>
    private Task HandleDeleteAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers["Mcp-Session-Id"].ToString();
        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = 400;
            return Task.CompletedTask;
        }

        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Signal.Dispose();
            _logger.LogInformation("Session terminated: {SessionId}", sessionId);
            context.Response.StatusCode = 204; // No Content
        }
        else
        {
            context.Response.StatusCode = 404;
        }

        return Task.CompletedTask;
    }

    #region Legacy SSE Protocol (deprecated, for backward compatibility)

    private async Task HandleLegacySseAsync(HttpContext context)
    {
        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Legacy SSE connection: {SessionId}", sessionId);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        await context.Response.Body.FlushAsync();

        var session = new StreamableSession { SessionId = sessionId };
        _sessions.TryAdd(sessionId, session);

        try
        {
            // Send endpoint event (legacy protocol)
            var endpointEvent = $"event: endpoint\ndata: /message?sessionId={sessionId}\n\n";
            await context.Response.WriteAsync(endpointEvent);
            await context.Response.Body.FlushAsync();

            while (!context.RequestAborted.IsCancellationRequested)
            {
                if (session.MessageQueue.TryDequeue(out var message))
                {
                    var sseMessage = $"event: message\ndata: {message}\n\n";
                    await context.Response.WriteAsync(sseMessage);
                    await context.Response.Body.FlushAsync();
                }
                else
                {
                    var signaled = await Task.Run(() => session.Signal.WaitOne(15000), context.RequestAborted);
                    if (!signaled)
                    {
                        await context.Response.WriteAsync(": heartbeat\n\n");
                        await context.Response.Body.FlushAsync();
                    }
                }
            }
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            session.Signal.Dispose();
            _logger.LogInformation("Legacy SSE session closed: {SessionId}", sessionId);
        }
    }

    private async Task HandleLegacyMessageAsync(HttpContext context)
    {
        var sessionId = context.Request.Query["sessionId"].ToString();
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out _))
        {
            _logger.LogWarning("Legacy message rejected for invalid session: {SessionId}", sessionId);
            context.Response.StatusCode = 400;
            return;
        }

        using var reader = new StreamReader(context.Request.Body);
        var message = await reader.ReadToEndAsync();

        _logger.LogInformation("Legacy POST /message from session {SessionId}", sessionId);

        if (_onMessageReceived != null)
        {
            await _onMessageReceived(message);
        }

        context.Response.StatusCode = 202;
    }

    #endregion

    private class StreamableSession
    {
        public required string SessionId { get; init; }
        public ConcurrentQueue<string> MessageQueue { get; } = new();
        public AutoResetEvent Signal { get; } = new(false);
    }
}
