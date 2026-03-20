#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Cataloger.Utility;

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

    /// <summary>
    /// Fetches from Mediator API using redundant base URLs.
    /// 3 attempts: (1) random base, (2) alternate base, (3) random base.
    /// </summary>
    internal async Task<string?> GetMediatorAsync(string pathAndQuery)
    {
        using var client = CreateHttpClient();
        var baseUrls = AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls;
        return await GetPubMediaLinksRetry.GetStringAsync(client, baseUrls, pathAndQuery);
    }

    internal async Task<string> GetAsync(string catalogLink)
    {
        if (TryGetAlternateJwCdnUrl(catalogLink, out var alternateUrl))
        {
            return await GetWithJwCdnHostRetryAsync(catalogLink, alternateUrl);
        }

        try
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                using var client = CreateHttpClient();
                return await SendRequestWithFallback(client, catalogLink);
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            throw;
        }
    }

    /// <summary>
    /// 3 attempts with host alternation: (1) random pick, (2) alternate host, (3) random pick.
    /// Each attempt uses HTTP/2 → HTTP/1.1 fallback.
    /// </summary>
    private async Task<string> GetWithJwCdnHostRetryAsync(string primaryUrl, string alternateUrl)
    {
        var first = Random.Shared.Next(2) == 0 ? primaryUrl : alternateUrl;
        var second = first == primaryUrl ? alternateUrl : primaryUrl;
        var third = Random.Shared.Next(2) == 0 ? primaryUrl : alternateUrl;
        var attempts = new[] { first, second, third };

        Exception? lastException = null;
        for (var i = 0; i < attempts.Length; i++)
        {
            try
            {
                using var client = CreateHttpClient();
                return await SendRequestWithFallback(client, attempts[i]);
            }
            catch (HttpRequestException ex) when (
                ex.Message.Contains("Response status code") &&
                !ex.Message.Contains("Server busy"))
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                lastException = ex;
                if (i < attempts.Length - 1)
                {
                    logger.Warning("Request failed for {Url}, retrying with alternate host: {Error}",
                        attempts[i], ex.Message);
                }
            }
        }

        throw lastException!;
    }

    private static bool TryGetAlternateJwCdnUrl(string url, out string alternateUrl)
    {
        if (url.Contains("b.jw-cdn.org"))
        {
            alternateUrl = url.Replace("https://b.jw-cdn.org", "https://app.jw-cdn.org");
            return true;
        }

        if (url.Contains("app.jw-cdn.org"))
        {
            alternateUrl = url.Replace("https://app.jw-cdn.org", "https://b.jw-cdn.org");
            return true;
        }

        alternateUrl = url;
        return false;
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

    private async Task<string> SendRequestWithFallback(HttpClient client, string catalogLink)
    {
        try
        {
            return await SendHttpRequest(client, catalogLink, new Version(2, 0), HttpVersionPolicy.RequestVersionOrHigher);
        }
        catch (HttpRequestException ex) when (!ex.Message.Contains("Server busy") && !ex.Message.Contains("Response status code"))
        {
            return await SendHttpRequest(client, catalogLink, new Version(1, 1), HttpVersionPolicy.RequestVersionExact);
        }
    }

    private async Task<string> SendHttpRequest(
        HttpClient client,
        string catalogLink,
        Version httpVersion,
        HttpVersionPolicy versionPolicy)
    {
        using var request = CreateHttpRequest(catalogLink, httpVersion, versionPolicy);
        var response = await client.SendAsync(request);
        ValidateResponse(response);
        return await response.Content.ReadAsStringAsync();
    }

    private static HttpRequestMessage CreateHttpRequest(string catalogLink, Version httpVersion, HttpVersionPolicy versionPolicy)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, catalogLink)
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
