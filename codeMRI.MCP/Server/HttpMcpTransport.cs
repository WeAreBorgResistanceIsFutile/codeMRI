using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using codeMRI.MCP.Protocol;

namespace codeMRI.MCP.Server;

/// <summary>
/// MCP transport implementation using HTTP/SSE
/// </summary>
public class HttpMcpTransport : IMcpTransport
{
    private readonly ILogger<HttpMcpTransport> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, SseSession> _sessions;
    private Func<string, Task>? _onMessageReceived;
    private readonly int _port;

    public HttpMcpTransport(ILogger<HttpMcpTransport> logger, int port = 8080)
    {
        _logger = logger;
        _port = port;
        _sessions = new ConcurrentDictionary<string, SseSession>();
        
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
        builder.Logging.SetMinimumLevel(LogLevel.Warning); // Less noise

        var app = builder.Build();

        app.MapGet("/sse", HandleSseAsync);
        app.MapPost("/message", HandleMessageAsync);

        _logger.LogInformation("HTTP transport listening on port {Port}", _port);
        
        await app.RunAsync($"http://0.0.0.0:{_port}");
    }

    public async Task SendMessageAsync(string message, CancellationToken ct)
    {
        foreach (var session in _sessions.Values)
        {
            session.ResponseQueue.Enqueue(message);
            session.Signal.Set();
        }
        await Task.CompletedTask;
    }

    private async Task HandleSseAsync(HttpContext context)
    {
        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("New SSE connection: {SessionId}", sessionId);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        await context.Response.Body.FlushAsync();

        var session = new SseSession { SessionId = sessionId };
        _sessions.TryAdd(sessionId, session);

        try
        {
            // Send endpoint event
            var endpointEvent = $"event: endpoint\ndata: /message?sessionId={sessionId}\n\n";
            await context.Response.WriteAsync(endpointEvent);
            await context.Response.Body.FlushAsync();

            while (!context.RequestAborted.IsCancellationRequested)
            {
                if (session.ResponseQueue.TryDequeue(out var message))
                {
                    var sseMessage = $"event: message\ndata: {message}\n\n";
                    await context.Response.WriteAsync(sseMessage);
                    await context.Response.Body.FlushAsync();
                }
                else
                {
                    // Use WaitOne with a timeout for heartbeats
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
            _logger.LogInformation("SSE session closed: {SessionId}", sessionId);
        }
    }

    private async Task HandleMessageAsync(HttpContext context)
    {
        var sessionId = context.Request.Query["sessionId"].ToString();
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Rejecting message for invalid session: {SessionId}", sessionId);
            context.Response.StatusCode = 400;
            return;
        }

        using var reader = new StreamReader(context.Request.Body);
        var message = await reader.ReadToEndAsync();
        
        _logger.LogInformation("Received POST message from session {SessionId}", sessionId);

        if (_onMessageReceived != null)
        {
            await _onMessageReceived(message);
        }

        context.Response.StatusCode = 202; // Accepted
    }

    private class SseSession
    {
        public required string SessionId { get; init; }
        public ConcurrentQueue<string> ResponseQueue { get; } = new();
        public AutoResetEvent Signal { get; } = new(false);
    }
}
