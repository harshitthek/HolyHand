using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace HolyHand.Core.Jev;

public class JevClient : IJevClient
{
    private readonly HttpClient _httpClient;
    private readonly JevOptions _options;
    private readonly ILogger<JevClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public JevClient(HttpClient httpClient, JevOptions options, ILogger<JevClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<EvaluateResponse> EvaluateAsync(EvaluateRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AuthException("API key is not configured. Set AI_GATEWAY_API_KEY in .env or environment.");
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/v1/evaluate";
        var requestJson = JsonSerializer.Serialize(request, JsonOpts);

        var stopwatch = Stopwatch.StartNew();
        int attempts = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            HttpResponseMessage response;
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Jev request was cancelled by kill switch / cancellation token.");
                throw;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning("Jev request timed out after {Seconds} seconds.", _options.TimeoutSeconds);
                if (attempts <= _options.MaxRetries)
                {
                    await BackoffDelayAsync(attempts, cancellationToken);
                    continue;
                }
                throw new TransientException("Request timed out after max retries.", 408, ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP network transport failure on attempt {Attempt}", attempts);
                if (attempts <= _options.MaxRetries)
                {
                    await BackoffDelayAsync(attempts, cancellationToken);
                    continue;
                }
                throw new TransientException($"Network failure: {ex.Message}", 0, ex);
            }

            stopwatch.Stop();
            var latencyMs = stopwatch.ElapsedMilliseconds;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Handle successful responses
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = JsonSerializer.Deserialize<EvaluateResponse>(responseBody, JsonOpts)
                        ?? throw new ProtocolException("Received null or empty evaluate response from Gateway.");

                    var cost = result.ProviderMetadata?.Gateway?.Cost;
                    var costStr = cost.HasValue ? $"${cost.Value:F6}" : "N/A";

                    _logger.LogInformation("Jev evaluation completed in {LatencyMs}ms | Cost: {Cost}", latencyMs, costStr);
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse evaluate response JSON: {Body}", responseBody);
                    throw new ProtocolException($"Failed to parse Gateway response: {ex.Message}", ex);
                }
            }

            var statusCode = (int)response.StatusCode;

            // 401 / 403: Authentication errors — DO NOT retry
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogError("Authentication rejected by Gateway (HTTP {StatusCode})", statusCode);
                throw new AuthException(responseBody, statusCode);
            }

            // Other 4xx client errors: Bad Request, Not Found, etc. — DO NOT retry
            if (statusCode is >= 400 and < 500 and not 429)
            {
                _logger.LogError("Client error from Gateway (HTTP {StatusCode}): {Body}", statusCode, responseBody);
                throw new ProtocolException($"Client request rejected (HTTP {statusCode}): {responseBody}");
            }

            // 429 (Rate Limit) or 5xx (Server Error): Retry with exponential backoff + jitter
            if (attempts <= _options.MaxRetries)
            {
                _logger.LogWarning("Transient gateway error (HTTP {StatusCode}). Retrying attempt {NextAttempt}/{MaxRetries}...",
                    statusCode, attempts + 1, _options.MaxRetries);

                await BackoffDelayAsync(attempts, cancellationToken);
                stopwatch.Restart();
                continue;
            }

            _logger.LogError("Exhausted retries on transient error (HTTP {StatusCode}): {Body}", statusCode, responseBody);
            throw new TransientException(responseBody, statusCode);
        }
    }

    private static async Task BackoffDelayAsync(int attempt, CancellationToken ct)
    {
        // Exponential backoff: 500ms, 1000ms, 2000ms... + random jitter up to 250ms
        var baseMs = (int)(Math.Pow(2, attempt - 1) * 500);
        var jitter = Random.Shared.Next(0, 250);
        await Task.Delay(baseMs + jitter, ct);
    }
}
