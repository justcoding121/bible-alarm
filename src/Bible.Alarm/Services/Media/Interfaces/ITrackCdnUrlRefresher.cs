#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Re-catalogs publication/section from JW APIs after CDN returns 404/410, then resolves the new track URL.
/// </summary>
public interface ITrackCdnUrlRefresher
{
    /// <summary>
    /// Fetches fresh track list from API (section or publication level), updates the media index, returns new CDN URL for the track.
    /// </summary>
    Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata, CancellationToken cancellationToken = default);
}
