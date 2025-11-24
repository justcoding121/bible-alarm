using System.Net;
using System.Net.Http.Headers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class DownloadService(HttpMessageHandler handler, ILogger logger) : IDownloadService
{
    private readonly int _timeOutSeconds = AppConstants.CacheSettings.DownloadTimeoutSeconds;
    private readonly ILogger _logger = logger;

    // User-Agent string to identify the app and prevent 403 errors from servers that block requests without proper User-Agent
    private const string UserAgent = "BibleAlarm/1.0 (compatible; iOS; MAUI)";

    private readonly AsyncRetryPolicy _downloadRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: AppConstants.CacheSettings.DownloadRetryAttempts,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
            onRetry: (_, _, _, _) =>
            {
                // Optional: Add logging here if needed
            });

    // Polly retry policy for file existence checks
    private readonly AsyncRetryPolicy _fileExistsRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: AppConstants.CacheSettings.FileExistsCheckRetryAttempts,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
            onRetry: (_, _, _, _) =>
            {
                // Optional: Add logging here if needed
            });

    public async Task<byte[]> DownloadAsync(string url, string alternativeUrl = null)
    {
        return await _downloadRetryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                
                using var client = new HttpClient(handler, false);
                using var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to download from primary URL: {Url}", url);
                
                if (alternativeUrl == null)
                {
                    _logger.Error(ex, "No alternative URL provided for failed download: {Url}", url);
                    throw;
                }

                _logger.Information("Attempting to download from alternative URL: {AlternativeUrl}", alternativeUrl);
                
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, alternativeUrl);
                    request.Headers.UserAgent.ParseAdd(UserAgent);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    
                    using var client = new HttpClient(handler, false);
                    using var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
                catch (Exception altEx)
                {
                    _logger.Error(altEx, "Failed to download from alternative URL: {AlternativeUrl}", alternativeUrl);
                    throw;
                }
            }
        });
    }


    public void Dispose()
    {
        handler.Dispose();
    }

    public async Task<bool> FileExists(string url)
    {
        return await _fileExistsRetryPolicy.ExecuteAsync(async () =>
        {
            using var client = new HttpClient(handler, false)
            {
                Timeout = TimeSpan.FromSeconds(_timeOutSeconds)
            };

            var getRequest = async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                
                var result = await client.SendAsync(request);
                var statusCode = result.StatusCode;

                if (statusCode == HttpStatusCode.OK) return true;

                return false;
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                
                var result = await client.SendAsync(request);
                var statusCode = result.StatusCode;

                if (statusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
                    return true;

                return await getRequest();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "HEAD request failed for URL: {Url}, falling back to GET request", url);
                return await getRequest();
            }
        });
    }
}