#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Fetches API responses with retry across redundant base URLs (b.jw-cdn.org, app.jw-cdn.org).
/// 3 attempts: (1) random base, (2) alternate base, (3) random base.
/// </summary>
public static class GetPubMediaLinksRetry
{
    /// <summary>
    /// Returns base URLs from constants for GETPUBMEDIALINKS.
    /// </summary>
    public static IReadOnlyList<string> GetBaseUrlsFromConstants()
    {
        return AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls;
    }

    /// <summary>
    /// 3 attempts with host alternation: (1) random pick, (2) alternate host, (3) random pick.
    /// Returns the first successful response body, or null if all attempts fail.
    /// </summary>
    public static async Task<string?> GetStringAsync(
        HttpClient httpClient,
        IEnumerable<string> baseUrls,
        string queryString,
        CancellationToken cancellationToken = default)
    {
        var urls = baseUrls as IReadOnlyList<string> ?? baseUrls.ToList();
        if (urls.Count == 0)
        {
            return null;
        }

        var attemptOrder =
            PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(urls.Count, max => Random.Shared.Next(max));

        foreach (var idx in attemptOrder)
        {
            try
            {
                var url = urls[idx].TrimEnd('/') + queryString;
                var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Debug(ex, "GetPubMediaLinksRetry: HTTP error on host index {HostIndex}, retrying", idx);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                Log.Debug(ex, "GetPubMediaLinksRetry: timeout on host index {HostIndex}, retrying", idx);
            }
        }

        return null;
    }
}
