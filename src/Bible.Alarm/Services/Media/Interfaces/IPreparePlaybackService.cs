#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPreparePlaybackService : IDisposable
{
    Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId);
    
    /// <summary>
    /// Prepares a single track by downloading it and creating an AudioPlayerTrack.
    /// Used for getting metadata for a single track without preparing the entire playlist.
    /// </summary>
    Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem);
}

