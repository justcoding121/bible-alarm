#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPreparePlaybackService : IDisposable
{
    Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId);
    Task DispatchPlaylistChangedAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex);
}

