#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

/// <inheritdoc />
public sealed class CdnPlaybackUrlProbe(HttpClient httpClient, ILogger logger) : ICdnPlaybackUrlProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(12);

    /// <inheritdoc />
    public async Task<CdnUrlProbeOutcome> ProbeStreamingUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            (!url.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase) &&
             !url.StartsWith(MediaUriSchemeConstants.HttpPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return CdnUrlProbeOutcome.Indeterminate;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);

            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            var headResponse = await httpClient.SendAsync(
                headRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            using (headResponse)
            {
                var code = (int)headResponse.StatusCode;
                if (code == 404 || code == 410)
                {
                    return CdnUrlProbeOutcome.NotFoundOrGone;
                }

                if (code is >= 200 and < 400)
                {
                    return CdnUrlProbeOutcome.ResourceReachable;
                }

                if (code == 405 || code == 501)
                {
                    return await ProbeWithRangeGetAsync(url, cts.Token);
                }

                if (code is >= 500 and < 600)
                {
                    logger.Debug(AppConstants.Logging.CdnPlaybackUrlProbeDiagnosticsLog.HeadReturnedStatusTreatingAsIndeterminate, code);
                    return CdnUrlProbeOutcome.Indeterminate;
                }

                return CdnUrlProbeOutcome.Indeterminate;
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.Debug(ex, AppConstants.Logging.CdnPlaybackUrlProbeDiagnosticsLog.ProbeTimedOutForUrl);
            return CdnUrlProbeOutcome.Indeterminate;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.CdnPlaybackUrlProbeDiagnosticsLog.ProbeFailedIndeterminate);
            return CdnUrlProbeOutcome.Indeterminate;
        }
    }

    private async Task<CdnUrlProbeOutcome> ProbeWithRangeGetAsync(string url, CancellationToken cancellationToken)
    {
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
        getRequest.Headers.Range = new RangeHeaderValue(0, 0);
        var response = await httpClient.SendAsync(
            getRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        using (response)
        {
            var code = (int)response.StatusCode;
            if (code == 404 || code == 410)
            {
                return CdnUrlProbeOutcome.NotFoundOrGone;
            }

            if (code is >= 200 and < 400 || code == 206)
            {
                return CdnUrlProbeOutcome.ResourceReachable;
            }

            return CdnUrlProbeOutcome.Indeterminate;
        }
    }
}
