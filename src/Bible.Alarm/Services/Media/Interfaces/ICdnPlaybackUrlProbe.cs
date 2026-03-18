#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Outcome of probing a CDN/stream URL (to distinguish stale 404 URLs from network/player errors).
/// </summary>
public enum CdnUrlProbeOutcome
{
    /// <summary>HTTP 404 or 410 — URL no longer valid; refresh from GETPUBMEDIALINKS/mediator.</summary>
    NotFoundOrGone,

    /// <summary>URL responds as present (2xx/3xx) — failure was likely network blip, codec, or player state.</summary>
    ResourceReachable,

    /// <summary>Timeout, connection error, or non-404 HTTP — do not re-catalog; user should retry playback.</summary>
    Indeterminate
}

/// <summary>
/// Probes a media URL to see if the server reports the file missing (404/410).
/// </summary>
public interface ICdnPlaybackUrlProbe
{
    Task<CdnUrlProbeOutcome> ProbeStreamingUrlAsync(string url, CancellationToken cancellationToken = default);
}
