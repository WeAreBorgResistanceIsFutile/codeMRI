using System.Text;
using Microsoft.Extensions.Logging;

namespace codeMRI.MCP.Server;

/// <summary>
/// MCP transport implementation using standard input/output
/// </summary>
public class StdioTransport : IMcpTransport
{
    private readonly ILogger<StdioTransport> _logger;
    private StreamWriter? _writer;

    public StdioTransport(ILogger<StdioTransport> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(Func<string, Task> onMessageReceived, CancellationToken ct)
    {
        _logger.LogInformation("Stdio transport starting. Redirected: In={In}, Out={Out}", 
            Console.IsInputRedirected, Console.IsOutputRedirected);

        _writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);

                if (line == null)
                {
                    _logger.LogInformation("Stdin reached EOF, closing transport");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                await onMessageReceived(line);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stdio transport cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Stdio transport");
            throw;
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken ct)
    {
        if (_writer == null)
        {
            _logger.LogWarning("Cannot send message: Stdio writer is not initialized");
            return;
        }

        await _writer.WriteLineAsync(message);
    }
}
