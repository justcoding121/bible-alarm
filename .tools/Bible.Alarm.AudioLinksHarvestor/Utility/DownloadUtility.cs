using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

internal class DownloadUtility
{
    private readonly ILogger logger;
    private readonly AsyncRetryPolicy<string> retryPolicy;

    public DownloadUtility(ILogger logger)
    {
        this.logger = logger;
        retryPolicy = Policy<string>
            .Handle<HttpRequestException>(ex =>
                ex.Message.Contains("Server busy") ||
                !ex.Message.Contains("Response status code"))
            .Or<TaskCanceledException>()
            .Or<IOException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    this.logger.Warning("Retrying request (attempt {RetryCount}/3) after {DelaySeconds}s delay...", retryCount, timespan.TotalSeconds);
                });
    }

    internal async Task<string> GetAsync(string harvestLink)
    {
        try
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                using var handler = new HttpClientHandler();
                handler.AllowAutoRedirect = true;
                handler.MaxAutomaticRedirections = 10;
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(60);

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, harvestLink)
                    {
                        Version = new Version(2, 0),
                        VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
                    };

                    request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; curl/8.0.1)");
                    request.Headers.Add("Accept", "application/json, text/plain, */*");

                    var response = await client.SendAsync(request);

                    if (response.StatusCode is HttpStatusCode.ServiceUnavailable or
                        HttpStatusCode.TooManyRequests)
                    {
                        throw new HttpRequestException($"Server busy: {response.StatusCode}");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException ex) when (!ex.Message.Contains("Server busy") && !ex.Message.Contains("Response status code"))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, harvestLink)
                    {
                        Version = new Version(1, 1),
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact
                    };

                    request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; curl/8.0.1)");
                    request.Headers.Add("Accept", "application/json, text/plain, */*");

                    var response = await client.SendAsync(request);

                    if (response.StatusCode is HttpStatusCode.ServiceUnavailable or
                        HttpStatusCode.TooManyRequests)
                    {
                        throw new HttpRequestException($"Server busy: {response.StatusCode}");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            throw;
        }
    }
}
