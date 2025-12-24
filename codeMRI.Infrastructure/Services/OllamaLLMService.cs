using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Services.MessageComposition;
using codeMRI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Infrastructure.Services;

/// <summary>
/// Ollama LLM service implementing both transport (ILLMClient) and validation (ILLMValidator).
/// ONLY handles HTTP communication and validation - NO message composition or truncation.
/// </summary>
public class OllamaLLMService : ILLMClient, ILLMValidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLMService> _logger;
    private readonly OllamaSettings _settings;

    public OllamaLLMService(
        HttpClient httpClient,
        IOptions<OllamaSettings> settings,
        ILogger<OllamaLLMService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        // Default to a long timeout (2 hours) to allow specific requests to control their own timeout via CancellationToken
        _httpClient.Timeout = TimeSpan.FromHours(2);
    }

    #region ILLMValidator Implementation

    public int ContextSize => _settings.ContextSize;
    public int ResponseBuffer => 2048; // Reserve for response

    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        
        // Conservative estimate: ~4 characters per token for technical text
        return text.Length / 4;
    }

    public MessageValidationResult ValidateMessages(List<ChatMessage> messages)
    {
        if (messages == null || !messages.Any())
        {
            return new MessageValidationResult
            {
                IsValid = true,
                EstimatedTokens = 0,
                AvailableTokens = ContextSize - ResponseBuffer
            };
        }

        var totalTokens = messages.Sum(m => EstimateTokenCount(m.Content ?? ""));
        var availableTokens = ContextSize - ResponseBuffer;
        var isValid = totalTokens <= availableTokens;

        return new MessageValidationResult
        {
            IsValid = isValid,
            EstimatedTokens = totalTokens,
            AvailableTokens = availableTokens,
            ErrorMessage = isValid
                ? null
                : $"Messages exceed context window: {totalTokens} tokens > {availableTokens} available " +
                  $"(ContextSize: {ContextSize}, ResponseBuffer: {ResponseBuffer}). " +
                  "Use ILLMServiceFacade for automatic message composition strategies."
        };
    }

    #endregion

    #region ILLMClient Implementation

    public async Task<string> ChatAsync(
        List<ChatMessage> messages,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        if (messages == null || !messages.Any())
            throw new ArgumentException("Messages cannot be null or empty", nameof(messages));

        // Validate messages fit in context
        var validation = ValidateMessages(messages);
        if (!validation.IsValid)
        {
            _logger.LogError(
                "Messages exceed context window: {EstimatedTokens} tokens > {AvailableTokens} available",
                validation.EstimatedTokens,
                validation.AvailableTokens);
            
            throw new InvalidOperationException(validation.ErrorMessage);
        }

        // Convert to Ollama format
        var ollamaMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList();

        var request = new
        {
            model = model ?? _settings.ChatModel,
            messages = ollamaMessages,
            stream = false,
            options = new
            {
                temperature = _settings.Temperature,
                num_ctx = _settings.ContextSize
            }
        };

        var requestJson = JsonSerializer.Serialize(request);
        var displayJson = requestJson.Length > 2000
            ? requestJson.Substring(0, 2000) + $"... [TRUNCATED {requestJson.Length - 2000} chars]"
            : requestJson;

        _logger.LogInformation(
            "Sending ChatAsync request to {Url} with {MessageCount} messages ({EstimatedTokens} tokens). Body: {Body}",
            "/api/chat",
            messages.Count,
            validation.EstimatedTokens,
            displayJson);

        // Retry logic with exponential backoff
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        const int maxDelayMs = 5000;
        var random = new Random();

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);

                if (IsTransientError(response.StatusCode) && attempt < maxRetries)
                {
                    var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                    var jitter = random.Next(0, exponentialDelay / 2);
                    var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);

                    _logger.LogWarning(
                        "Transient error {StatusCode} on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...",
                        response.StatusCode, attempt, maxRetries, delay);
                    
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var contentString = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    if (attempt < maxRetries)
                    {
                        var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                        var jitter = random.Next(0, exponentialDelay / 2);
                        var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);

                        _logger.LogWarning(
                            "Ollama returned empty response on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...",
                            attempt, maxRetries, delay);
                        
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    _logger.LogError(
                        "Ollama returned empty response after {MaxRetries} attempts. Status Code: {StatusCode}",
                        maxRetries, response.StatusCode);
                    
                    throw new HttpRequestException(
                        $"Ollama returned an empty response after {maxRetries} attempts (Status: {response.StatusCode})");
                }

                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(contentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (ollamaResponse?.Message?.Content == null)
                {
                    _logger.LogError(
                        "Failed to parse Ollama response or message content is null. Raw response: {Response}",
                        contentString.Length > 500 ? contentString.Substring(0, 500) + "..." : contentString);
                    
                    throw new InvalidOperationException("Failed to parse Ollama response or message content is null");
                }

                _logger.LogInformation(
                    "Received response from Ollama ({ResponseLength} chars)",
                    ollamaResponse.Message.Content.Length);

                return ollamaResponse.Message.Content;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("ChatAsync operation canceled or timed out after {MaxRetries} attempts.", maxRetries);
                throw;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                var exponentialDelay = baseDelayMs * (1 << (attempt - 1));
                var jitter = random.Next(0, exponentialDelay / 2);
                var delay = Math.Min(exponentialDelay + jitter, maxDelayMs);

                _logger.LogWarning(
                    "HTTP request failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...",
                    attempt, maxRetries, delay);
                
                await Task.Delay(delay, cancellationToken);
            }
        }

        _logger.LogError("Max retries ({MaxRetries}) exceeded for Ollama API.", maxRetries);
        throw new HttpRequestException(
            $"Max retries ({maxRetries}) exceeded for Ollama API. The service may be experiencing issues.");
    }

    #endregion

    #region Helper Methods

    private static bool IsTransientError(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.InternalServerError ||
               statusCode == HttpStatusCode.BadGateway ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout;
    }

    #endregion

    #region Response DTOs

    private class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        public string? Content { get; set; }
    }

    #endregion
}