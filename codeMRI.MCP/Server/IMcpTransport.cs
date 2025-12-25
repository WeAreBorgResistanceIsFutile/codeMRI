namespace codeMRI.MCP.Server;

/// <summary>
/// Defines a transport layer for MCP communication (e.g., stdio, HTTP/SSE)
/// </summary>
public interface IMcpTransport
{
    /// <summary>
    /// Starts the transport listener
    /// </summary>
    /// <param name="onMessageReceived">Callback for when a JSON-RPC message is received</param>
    /// <param name="ct">Cancellation token</param>
    Task StartAsync(Func<string, Task> onMessageReceived, CancellationToken ct);

    /// <summary>
    /// Sends a JSON-RPC message via the transport
    /// </summary>
    /// <param name="message">JSON-RPC message string</param>
    /// <param name="ct">Cancellation token</param>
    Task SendMessageAsync(string message, CancellationToken ct);
}
