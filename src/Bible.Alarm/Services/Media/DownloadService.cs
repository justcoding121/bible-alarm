using System.Net;
using System.Net.Http.Headers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class DownloadService(HttpMessageHandler handler, ILogger logger) : IDownloadService, IDisposable
{
    private readonly int timeOutSeconds = AppConstants.CacheSettings.DownloadTimeoutSeconds;
    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    private bool isDisposed;

    // User-Agent string to identify the app and prevent 403 errors from servers that block requests without proper User-Agent
    private const string UserAgent = "BibleAlarm/1.0 (compatible; iOS; MAUI)";

    private readonly AsyncRetryPolicy<byte[]> downloadRetryPolicy = Policy<byte[]>
            .Handle<Exception>(ex =>
            {
                // Never retry on cancellation - let it propagate immediately
                if (ex is OperationCanceledException)
                {
                    return false;
                }

                // Don't retry on HTTP errors like 403, 404 (permanent failures)
                if (ex is not HttpRequestException httpEx)
                {
                    return true;
                }

                var message = httpEx.Message;
                // Check for permanent HTTP errors that shouldn't be retried
                if (!message.Contains("403") &&
                    !message.Contains("404") &&
                    !message.Contains("Forbidden") &&
                    !message.Contains("Not Found"))
                {
                    return true;
                }

                logger.Debug("Skipping retry for permanent HTTP error: {Message}", message);
                // Don't handle/retry this exception
                return false;
                // Handle/retry other exceptions
            })
            .WaitAndRetryAsync(
                retryCount: AppConstants.CacheSettings.DownloadRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, _) =>
                {
                    // Log retry attempts
                    var exception = outcome?.Exception;
                    var exceptionMessage = exception?.Message ?? "Unknown error";
                    logger.Warning(exception, "Retrying download (attempt {RetryCount}/{MaxRetries}) after {DelaySeconds}s: {ExceptionMessage}",
                        retryCount,
                        AppConstants.CacheSettings.DownloadRetryAttempts,
                        timespan.TotalSeconds,
                        exceptionMessage);
                });

    // Polly retry policy for file existence checks
    private readonly AsyncRetryPolicy fileExistsRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: AppConstants.CacheSettings.FileExistsCheckRetryAttempts,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
            onRetry: (_, _, _, _) =>
            {
                // Optional: Add logging here if needed
            });

    public async Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null, CancellationToken cancellationToken = default)
    {
        // Combine the service's cancellation token with the provided one
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
        
        return await downloadRetryPolicy.ExecuteAsync(async ct =>
        {
            // Check for cancellation before starting download
            combinedCts.Token.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

                using var client = new HttpClient(handler, false);
                using var response = await client.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(ct);
            }
            catch (OperationCanceledException)
            {
                logger.Information("Download cancelled for URL: {Url}", url);
                throw; // Re-throw cancellation immediately - Polly won't retry due to Handle condition
            }
            catch (Exception ex)
            {
                // Check for cancellation before retrying
                combinedCts.Token.ThrowIfCancellationRequested();

                logger.Warning(ex, "Failed to download from primary URL: {Url}", url);

                if (alternativeUrl == null)
                {
                    logger.Error(ex, "No alternative URL provided for failed download: {Url}", url);
                    throw;
                }

                logger.Information("Attempting to download from alternative URL: {AlternativeUrl}", alternativeUrl);

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, alternativeUrl);
                    request.Headers.UserAgent.ParseAdd(UserAgent);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

                    using var client = new HttpClient(handler, false);
                    using var response = await client.SendAsync(request, ct);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    logger.Information("Download cancelled for alternative URL: {AlternativeUrl}", alternativeUrl);
                    throw; // Re-throw cancellation immediately - Polly won't retry due to Handle condition
                }
                catch (Exception altEx)
                {
                    logger.Error(altEx, "Failed to download from alternative URL: {AlternativeUrl}", alternativeUrl);
                    throw;
                }
            }
        }, combinedCts.Token);
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
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

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
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

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
                logger.Warning(ex, "HEAD request failed for URL: {Url}, falling back to GET request", url);
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
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // HttpMessageHandler is registered as a singleton and should not be disposed here
        // It will be disposed by MauiAppHolder.Dispose()
    }
}
