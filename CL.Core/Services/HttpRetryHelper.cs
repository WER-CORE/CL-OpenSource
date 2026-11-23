using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public static class HttpRetryHelper
    {
        public const int DefaultMaxAttempts = 3;
        public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan DefaultMaxRetryAfter = TimeSpan.FromSeconds(15);

        public static async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            Func<HttpRequestMessage> requestFactory,
            CancellationToken token = default,
            int maxAttempts = DefaultMaxAttempts,
            TimeSpan? baseDelay = null,
            TimeSpan? maxRetryAfter = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
            Func<double>? jitterProvider = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (requestFactory == null) throw new ArgumentNullException(nameof(requestFactory));
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            TimeSpan effectiveBaseDelay = baseDelay ?? DefaultBaseDelay;
            TimeSpan effectiveMaxRetryAfter = maxRetryAfter ?? DefaultMaxRetryAfter;
            Func<TimeSpan, CancellationToken, Task> effectiveDelay = delayAsync ?? ((d, ct) => Task.Delay(d, ct));
            Func<double> effectiveJitter = jitterProvider ?? (() => Random.Shared.NextDouble());

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();

                HttpResponseMessage? response = null;
                try
                {
                    using var request = requestFactory();
                    response = await client.SendAsync(request, token).ConfigureAwait(false);

                    if (!IsTransientStatusCode(response.StatusCode))
                    {
                        return response;
                    }

                    TimeSpan? retryAfter = TryGetRetryAfterDelay(response.Headers.RetryAfter, DateTimeOffset.UtcNow);
                    if (retryAfter.HasValue && retryAfter.Value > effectiveMaxRetryAfter)
                    {
                        var statusCode = response.StatusCode;
                        response.Dispose();
                        response = null;
                        throw new HttpRequestException(
                            $"HTTP {(int)statusCode} ({statusCode}): Retry-After ({retryAfter.Value.TotalSeconds:F0}s) exceeds maximum allowed wait ({effectiveMaxRetryAfter.TotalSeconds:F0}s).",
                            null,
                            statusCode);
                    }

                    if (attempt == maxAttempts)
                    {
                        return response;
                    }

                    TimeSpan delay = ComputeDelay(attempt, effectiveBaseDelay, retryAfter, effectiveJitter);
                    response.Dispose();
                    response = null;

                    token.ThrowIfCancellationRequested();
                    await effectiveDelay(delay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    response?.Dispose();
                    throw;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested && attempt < maxAttempts)
                {
                    response?.Dispose();
                    TimeSpan delay = ComputeDelay(attempt, effectiveBaseDelay, null, effectiveJitter);
                    await effectiveDelay(delay, token).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (IsTransientException(ex) && !ex.Message.Contains("Retry-After", StringComparison.Ordinal) && attempt < maxAttempts)
                {
                    response?.Dispose();
                    TimeSpan delay = ComputeDelay(attempt, effectiveBaseDelay, null, effectiveJitter);
                    await effectiveDelay(delay, token).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Unreachable retry loop state.");
        }

        public static async Task<string> GetStringAsync(
            HttpClient client,
            string url,
            string? userAgent = null,
            CancellationToken token = default,
            int maxAttempts = DefaultMaxAttempts,
            TimeSpan? baseDelay = null,
            TimeSpan? maxRetryAfter = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
            Func<double>? jitterProvider = null)
        {
            using var response = await SendAsync(
                client,
                () =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    if (!string.IsNullOrWhiteSpace(userAgent))
                    {
                        req.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    }
                    return req;
                },
                token,
                maxAttempts,
                baseDelay,
                maxRetryAfter,
                delayAsync,
                jitterProvider).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }

        public static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }

        private static bool IsTransientException(HttpRequestException ex)
        {
            return !ex.StatusCode.HasValue || IsTransientStatusCode(ex.StatusCode.Value);
        }

        internal static TimeSpan? TryGetRetryAfterDelay(RetryConditionHeaderValue? header, DateTimeOffset nowUtc)
        {
            if (header == null)
                return null;

            if (header.Delta.HasValue)
                return header.Delta.Value < TimeSpan.Zero ? TimeSpan.Zero : header.Delta.Value;

            if (header.Date.HasValue)
            {
                var diff = header.Date.Value.ToUniversalTime() - nowUtc;
                return diff < TimeSpan.Zero ? TimeSpan.Zero : diff;
            }

            return null;
        }

        private static TimeSpan ComputeDelay(int attempt, TimeSpan baseDelay, TimeSpan? retryAfter, Func<double> jitterProvider)
        {
            double multiplier = Math.Pow(2, attempt - 1);
            double backoffMs = baseDelay.TotalMilliseconds * multiplier;
            double jitterMs = Math.Clamp(jitterProvider(), 0.0, 1.0) * 250.0;
            TimeSpan backoff = TimeSpan.FromMilliseconds(backoffMs + jitterMs);

            if (retryAfter.HasValue && retryAfter.Value > backoff)
            {
                return retryAfter.Value;
            }

            return backoff;
        }
    }
}
