#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPreparePlaybackService
{
    Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the URI for a single track (cached file or CDN URL for streaming).
    /// </summary>
    Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default);
}
