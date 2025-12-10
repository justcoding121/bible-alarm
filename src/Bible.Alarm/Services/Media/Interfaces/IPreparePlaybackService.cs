#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPreparePlaybackService : IDisposable
{
    Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId);
    
    /// <summary>
    /// Prepares a single track by downloading it and creating an AudioPlayerTrack.
    /// Used for getting metadata for a single track without preparing the entire playlist.
    /// </summary>
    /// <param name="playItem">The PlayItem to prepare</param>
    /// <returns>AudioPlayerTrack with downloaded URI, or null if download fails</returns>
    Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem);
}

