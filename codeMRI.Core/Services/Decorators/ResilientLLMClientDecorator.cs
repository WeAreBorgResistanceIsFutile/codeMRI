using System.Net;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services.Decorators;

/// <summary>
///     Decorator for ILLMClient that adds resilience through retries with exponential backoff and jitter.
/// </summary>
public class ResilientLLMClientDecorator : ILLMClient
{
    private readonly ILLMClient _inner;
    private readonly ILogger<ResilientLLMClientDecorator> _logger;
    private readonly RetrySettings _settings;
    private readonly Random _random;

    public ResilientLLMClientDecorator(
        ILLMClient inner,
        ILogger<ResilientLLMClientDecorator> logger,
        IOptions<RetrySettings> settings)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _random = new Random();
    }

    public async Task<string> ChatAsync(List<ChatMessage> messages, string? model = null, CancellationToken cancellationToken = default)
    {
        var attempt = 1;
        
        while (true)
        {
            try
            {
                var result = await _inner.ChatAsync(messages, model, cancellationToken);
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Successfully recovered from previous failures after {Attempt} attempts.", attempt);
                }

                return result;
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt <= _settings.MaxRetries)
            {
                var delay = CalculateDelay(attempt);
                
                _logger.LogWarning(ex, 
                    "Transient error on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms... Error: {ErrorMessage}",
                    attempt, _settings.MaxRetries, delay, ex.Message);
                
                await Task.Delay(delay, cancellationToken);
                attempt++;
            }
            catch (Exception ex) when (attempt > _settings.MaxRetries)
            {
                _logger.LogError(ex, "Max retries ({MaxRetries}) exceeded. Giving up.", _settings.MaxRetries);
                throw; // Rethrow the last exception
            }
        }
    }

    private int CalculateDelay(int attempt)
    {
        // Exponential backoff: Base * 2^(attempt-1)
        var exponentialDelay = _settings.BaseDelayMilliseconds * (1 << (attempt - 1));
        
        // Jitter: Random value between 0 and 50% of the calculated delay
        var jitter = _random.Next(0, exponentialDelay / 2);
        
        // Cap at MaxDelay
        return Math.Min(exponentialDelay + jitter, _settings.MaxDelayMilliseconds);
    }

    private bool IsTransientError(Exception ex)
    {
        // Check for generic HttpRequests which usually wrap the status code
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode == HttpStatusCode.RequestTimeout ||
                   httpEx.StatusCode == HttpStatusCode.TooManyRequests ||
                   httpEx.StatusCode == HttpStatusCode.InternalServerError ||
                   httpEx.StatusCode == HttpStatusCode.BadGateway ||
                   httpEx.StatusCode == HttpStatusCode.ServiceUnavailable ||
                   httpEx.StatusCode == HttpStatusCode.GatewayTimeout;
        }

        // Also handle standard TimeoutException
        if (ex is TimeoutException) return true;
        
        // Handle "TaskCanceledException" which is often a timeout, ONLY if NOT cancelled by the passed token
        if (ex is TaskCanceledException) return true; 

        return false;
    }
}
