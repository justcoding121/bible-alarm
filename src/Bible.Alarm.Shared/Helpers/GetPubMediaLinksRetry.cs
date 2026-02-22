#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Fetches GETPUBMEDIALINKS responses with retry across redundant base URLs (b.jw-cdn.org, app.jw-cdn.org).
/// Tries each URL in random order so load is spread.
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
    /// Tries each base URL in random order; returns the first successful response body, or null if all fail.
    /// </summary>
    /// <param name="httpClient">HttpClient to use</param>
    /// <param name="baseUrls">Full base URLs (e.g. https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS)</param>
    /// <param name="queryString">Query string including leading '?' (e.g. "?output=json&pub=thv&fileformat=MP4&alllangs=0&langwritten=E")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response body as string, or null if every base URL failed</returns>
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

        var order = urls.Count == 1 ? urls : urls.OrderBy(_ => Guid.NewGuid()).ToList();
        foreach (var baseUrl in order)
        {
            try
            {
                var url = baseUrl.TrimEnd('/') + queryString;
                var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException)
            {
                continue;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        return null;
    }
}
