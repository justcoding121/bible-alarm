#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Returns the track CDN URL when it is already available (e.g. from TrackUrl).
/// Track CDN URLs are obtained from section/pub fetch responses and stored in TrackUrl; no per-track fetch.
/// </summary>
public sealed class MediaUrlRefreshService(ILogger logger) : IMediaUrlRefreshService
{
    public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata)
    {
        var lookUpPath = trackMetadata.LookUpPath;
        if (string.IsNullOrEmpty(lookUpPath))
        {
            return Task.FromResult<string?>(null);
        }

        if (lookUpPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<string?>(lookUpPath);
        }

        logger.Debug("RefreshUrlAsync: LookUpPath is not a CDN URL (no individual track fetch). Track may need section/pub fetch first.");
        return Task.FromResult<string?>(null);
    }
}
