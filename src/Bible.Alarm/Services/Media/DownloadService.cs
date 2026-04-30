#nullable enable
using System.Net;
using System.Net.Http.Headers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class DownloadService(HttpMessageHandler handler, ILogger logger) : IDownloadService, IDisposable
{
    private readonly int timeOutSeconds = AppConstants.CacheSettings.DownloadTimeoutSeconds;
    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    private bool isDisposed;

    private readonly AsyncRetryPolicy<byte[]> downloadRetryPolicy = Policy<byte[]>
            .Handle<Exception>(ex =>
            {
                // Never retry on cancellation - let it propagate immediately
                if (ex is OperationCanceledException)
                {
                    return false;
                }

                // Don't retry on network connectivity failures (no internet, DNS failure)
                // Retrying won't help and causes UI to hang during delays (2s, 4s, etc.)
                if (NetworkExceptionHelper.IsNetworkFailure(ex))
                {
                    logger.Debug(AppConstants.Logging.DownloadDiagnosticsLog.SkippingRetryNetworkConnectivityFailure, ex.Message);
                    return false;
                }

                // Don't retry on HTTP errors like 403, 404 (permanent failures)
                if (ex is not HttpRequestException httpEx)
                {
                    return true;
                }

                var message = httpEx.Message;
                // Check for permanent HTTP errors that shouldn't be retried
                if (!message.Contains(AppConstants.Media.DownloadPermanentFailureHttpFragments.StatusCode403, StringComparison.Ordinal) &&
                    !message.Contains(AppConstants.Media.DownloadPermanentFailureHttpFragments.StatusCode404, StringComparison.Ordinal) &&
                    !message.Contains(AppConstants.Media.DownloadPermanentFailureHttpFragments.Forbidden, StringComparison.Ordinal) &&
                    !message.Contains(AppConstants.Media.DownloadPermanentFailureHttpFragments.NotFound, StringComparison.Ordinal))
                {
                    return true;
                }

                logger.Debug(AppConstants.Logging.DownloadDiagnosticsLog.SkippingRetryPermanentHttpError, message);
                return false;
            })
            .WaitAndRetryAsync(
                retryCount: AppConstants.CacheSettings.DownloadRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, _) =>
                {
                    // Log retry attempts
                    var exception = outcome?.Exception;
                    var exceptionMessage = exception?.Message ?? AppConstants.Logging.UnknownErrorFallback;
                    logger.Warning(exception, AppConstants.Logging.DownloadDiagnosticsLog.RetryingDownloadAttemptAfterDelay,
                        retryCount,
                        AppConstants.CacheSettings.DownloadRetryAttempts,
                        timespan.TotalSeconds,
                        exceptionMessage);
                });

    // Polly retry policy for file existence checks (HEAD/GET requests)
    private readonly AsyncRetryPolicy fileExistsRetryPolicy = Policy
        .Handle<Exception>(ex => NetworkExceptionHelper.IsRetryableForNetworkOperation(ex))
        .WaitAndRetryAsync(
            retryCount: AppConstants.CacheSettings.FileExistsCheckRetryAttempts,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
            onRetry: (_, _, _, _) =>
            {
                // Optional: Add logging here if needed
            });

    public async Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null, CancellationToken cancellationToken = default)
    {
        return await DownloadWithProgressAsyncInternal(url, null, cancellationToken, alternativeUrl);
    }

    public async Task<byte[]> DownloadWithProgressAsync(string url, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default)
    {
        return await DownloadWithProgressAsyncInternal(url, progressCallback, cancellationToken, null);
    }

    private async Task<byte[]> DownloadWithProgressAsyncInternal(string url, Action<long, long?>? progressCallback, CancellationToken cancellationToken, string? alternativeUrl)
    {
        // Combine the service's cancellation token with the provided one
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);

        return await downloadRetryPolicy.ExecuteAsync(async ct =>
        {
            // Check for cancellation before starting download
            combinedCts.Token.ThrowIfCancellationRequested();

            try
            {
                return await DownloadWithStallTimeoutAsync(url, combinedCts.Token, progressCallback);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation immediately - Polly won't retry due to Handle condition
            }
            catch (Exception ex)
            {
                // Check for cancellation before retrying
                combinedCts.Token.ThrowIfCancellationRequested();

                // If alternative URL is provided, try it before giving up
                if (!string.IsNullOrEmpty(alternativeUrl) && alternativeUrl != url)
                {
                    logger.Warning(ex, AppConstants.Logging.DownloadDiagnosticsLog.FailedToDownloadPrimaryTryingAlternative, url, alternativeUrl);
                    try
                    {
                        return await DownloadWithStallTimeoutAsync(alternativeUrl, combinedCts.Token, progressCallback);
                    }
                    catch (Exception altEx)
                    {
                        throw new InvalidOperationException(
                            $"Failed to download from alternative URL: {alternativeUrl}",
                            altEx);
                    }
                }

                throw;
            }
        }, combinedCts.Token);
    }

    public async Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken = default)
    {
        // Combine the service's cancellation token with the provided one
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);

        return await fileExistsRetryPolicy.ExecuteAsync(async ct =>
        {
            using var client = new HttpClient(handler, false)
            {
                Timeout = TimeSpan.FromSeconds(timeOutSeconds)
            };

            try
            {
                // Try HEAD request first (lightweight)
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                headRequest.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
                headRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));

                var headResponse = await client.SendAsync(headRequest, ct);
                if (headResponse.IsSuccessStatusCode)
                {
                    return headResponse.Content.Headers.ContentLength;
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, AppConstants.Logging.DownloadDiagnosticsLog.HeadRequestFailedTryingGetHeadersOnly, url);
            }

            // Fallback: GET request but only read headers (no body download)
            try
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
                getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));

                using var response = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.IsSuccessStatusCode)
                {
                    return response.Content.Headers.ContentLength;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.DownloadDiagnosticsLog.FailedToGetContentLengthForUrl, url);
            }

            return null;
        }, combinedCts.Token);
    }

    /// <summary>
    /// Downloads a file with a stall timeout - only times out if no data is received for X seconds.
    /// This prevents canceling slow but active downloads while still detecting stalled connections.
    /// </summary>
    private async Task<byte[]> DownloadWithStallTimeoutAsync(string url, CancellationToken cancellationToken, Action<long, long?>? progressCallback = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));

        using var client = new HttpClient(handler, false);
        // No total timeout - we use stall detection instead
        client.Timeout = Timeout.InfiniteTimeSpan;

        // Get response headers first (fast operation, use connection timeout)
        using var connectionTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectionTimeoutCts.CancelAfter(TimeSpan.FromSeconds(timeOutSeconds));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectionTimeoutCts.Token);
        response.EnsureSuccessStatusCode();

        // Get content length if available for progress reporting
        var totalBytes = response.Content.Headers.ContentLength;

        // Stream the content with stall detection
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memoryStream = new MemoryStream();

        var buffer = new byte[8192];
        int bytesRead;
        long totalBytesRead = 0;
        var lastProgressReport = DateTime.MinValue;
        const int ProgressReportIntervalMs = 100; // Throttle progress reports

        // Report initial progress
        progressCallback?.Invoke(0, totalBytes);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create a new timeout for each read operation - resets on each successful read
            using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeoutCts.CancelAfter(TimeSpan.FromSeconds(timeOutSeconds));

            try
            {
                bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, readTimeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Read timeout (stall detected), not user cancellation
                throw new TimeoutException($"Download stalled - no data received for {timeOutSeconds} seconds");
            }

            if (bytesRead == 0)
            {
                break; // End of stream
            }

            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            // Report progress with throttling to avoid flooding UI
            var now = DateTime.UtcNow;
            if (progressCallback != null && (now - lastProgressReport).TotalMilliseconds >= ProgressReportIntervalMs)
            {
                progressCallback.Invoke(totalBytesRead, totalBytes);
                lastProgressReport = now;
            }
        }

        // Report final progress
        progressCallback?.Invoke(totalBytesRead, totalBytes);

        return memoryStream.ToArray();
    }

    public async Task<bool> FileExists(string url)
    {
        return await fileExistsRetryPolicy.ExecuteAsync(async ct =>
        {
            using var client = new HttpClient(handler, false)
            {
                Timeout = TimeSpan.FromSeconds(timeOutSeconds)
            };

            var getRequest = async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));

                var result = await client.SendAsync(request, ct);
                var statusCode = result.StatusCode;

                if (statusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                return false;
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));

                var result = await client.SendAsync(request, ct);
                var statusCode = result.StatusCode;

                if (statusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
                {
                    return true;
                }

                return await getRequest();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.DownloadDiagnosticsLog.HeadRequestFailedFallingBackToGet, url);
                return await getRequest();
            }
        }, cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // HttpMessageHandler is registered as a singleton and should not be disposed here
        // It will be disposed by MauiAppHolder.Dispose()
    }
}
