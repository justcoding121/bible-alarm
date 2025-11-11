using System.Net;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Shared.Constants;
using Polly;
using Polly.Retry;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Download service
/// </summary>
public class DownloadService(HttpMessageHandler handler) : IDownloadService
{
    private readonly int _timeOutSeconds = AppConstants.CacheSettings.DownloadTimeoutSeconds;

    // Polly retry policy with exponential backoff (starts at 1 second, doubles each retry)
    private readonly AsyncRetryPolicy _downloadRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: AppConstants.CacheSettings.DownloadRetryAttempts,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                // Optional: Add logging here if needed
            });

    // Polly retry policy for file existence checks
    private readonly AsyncRetryPolicy _fileExistsRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: AppConstants.CacheSettings.FileExistsCheckRetryAttempts,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                // Optional: Add logging here if needed
            });

    /// <summary>
    /// Dowload the file from the Url
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public async Task<byte[]> DownloadAsync(string url, string alternativeUrl = null)
    {
        return await _downloadRetryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                using var client = new HttpClient(handler, false);
                return await client.GetByteArrayAsync(url);
            }
            catch
            {
                if (alternativeUrl == null) throw;

                using var client = new HttpClient(handler, false);
                return await client.GetByteArrayAsync(alternativeUrl);
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
                var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
                var statusCode = result.StatusCode;

                if (statusCode == HttpStatusCode.OK) return true;

                return false;
            };

            try
            {
                var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                var statusCode = result.StatusCode;

                if (statusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
                    return true;

                return await getRequest();
            }
            catch
            {
                return await getRequest();
            }
        });
    }
}