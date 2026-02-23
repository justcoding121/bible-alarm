#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaUrlRefreshService
{
    /// <summary>
    /// Returns the track CDN URL when already available (e.g. LookUpPath set from TrackUrl.Url).
    /// Does not perform per-track fetch; CDN URLs come from section/pub response and are stored in TrackUrl.
    /// </summary>
    Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata);
}

