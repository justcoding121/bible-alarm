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
                onRetry: (_, timespan, retryCount, _) =>
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
                using var client = CreateHttpClient();
                return await SendRequestWithFallback(client, harvestLink);
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            throw;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        return client;
    }

    private async Task<string> SendRequestWithFallback(HttpClient client, string harvestLink)
    {
        try
        {
            return await SendHttpRequest(client, harvestLink, new Version(2, 0), HttpVersionPolicy.RequestVersionOrHigher);
        }
        catch (HttpRequestException ex) when (!ex.Message.Contains("Server busy") && !ex.Message.Contains("Response status code"))
        {
            return await SendHttpRequest(client, harvestLink, new Version(1, 1), HttpVersionPolicy.RequestVersionExact);
        }
    }

    private async Task<string> SendHttpRequest(
        HttpClient client,
        string harvestLink,
        Version httpVersion,
        HttpVersionPolicy versionPolicy)
    {
        using var request = CreateHttpRequest(harvestLink, httpVersion, versionPolicy);
        var response = await client.SendAsync(request);
        ValidateResponse(response);
        return await response.Content.ReadAsStringAsync();
    }

    private static HttpRequestMessage CreateHttpRequest(string harvestLink, Version httpVersion, HttpVersionPolicy versionPolicy)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, harvestLink)
        {
            Version = httpVersion,
            VersionPolicy = versionPolicy
        };

        request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; curl/8.0.1)");
        request.Headers.Add("Accept", "application/json, text/plain, */*");

        return request;
    }

    private static void ValidateResponse(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException($"Server busy: {response.StatusCode}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");
        }
    }
}
