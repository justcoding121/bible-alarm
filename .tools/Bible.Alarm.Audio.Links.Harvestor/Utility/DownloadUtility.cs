using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace Bible.Alarm.Audio.Links.Harvestor.Utility
{
    internal class DownloadUtility
    {
        private static readonly AsyncRetryPolicy<string> RetryPolicy = Policy<string>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<System.IO.IOException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)), // Exponential backoff: 1s, 2s, 4s
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Retrying request (attempt {retryCount}/3) after {timespan.TotalSeconds}s delay...");
                });

        internal static async Task<string> GetAsync(string harvestLink)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                using var handler = new HttpClientHandler();
                handler.AllowAutoRedirect = true;
                handler.MaxAutomaticRedirections = 10;
                handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli;

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(60);

                // Try HTTP/2 first (avoids TLS renegotiation issues), fallback to HTTP/1.1 if needed
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
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException)
                {
                    // If HTTP/2 fails, try HTTP/1.1 as fallback
                    using var request = new HttpRequestMessage(HttpMethod.Get, harvestLink)
                    {
                        Version = new Version(1, 1),
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact
                    };

                    request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; curl/8.0.1)");
                    request.Headers.Add("Accept", "application/json, text/plain, */*");

                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            });
        }
    }
}
