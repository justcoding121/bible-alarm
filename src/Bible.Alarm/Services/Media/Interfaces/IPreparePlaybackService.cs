#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPreparePlaybackService
{
    Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a single track by downloading it and creating an AudioPlayerTrack.
    /// Used for getting metadata for a single track without preparing the entire playlist.
    /// </summary>
    Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a single track with download progress reporting.
    /// </summary>
    /// <param name="playItem">The play item to download.</param>
    /// <param name="progressCallback">Called with (bytesDownloaded, totalBytes). totalBytes may be null if unknown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AudioPlayerTrack?> PrepareSingleTrackWithProgressAsync(
        PlayItem playItem,
        Action<long, long?>? progressCallback,
        CancellationToken cancellationToken = default);
}

