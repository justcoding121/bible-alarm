#nullable enable
using Bible.Alarm.Platforms.Android.Services.Media.Session;
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Platforms.Android.Services.Media.Interfaces;

/// <summary>
/// Manager for the shared media session used by Android Auto and system controls.
/// </summary>
public interface IMediaSessionManager
{
    /// <summary>True when the session exists. Creating it is part of the check, matching GetOrCreate.</summary>
    bool IsAvailable { get; }

    SessionPlaybackSnapshot Snapshot { get; }

    void UpdatePlaybackState(SessionPlaybackState state, long position = 0, bool canPlayNext = false, bool canPlayPrevious = false);
    void UpdatePlaybackPosition(TimeSpan position, TimeSpan duration, bool canPlayNext = false, bool canPlayPrevious = false);
    void UpdateMetadata(string title, string artist, string? album = null, int? scheduleId = null, string? artworkUrl = null, TimeSpan? duration = null);
    void SetBufferingStateOnly();
    void UpdatePlaybackStateForStop();
    void SetPlaybackStatus(PlayStatus status, bool canPlayNext = false, bool canPlayPrevious = false);
    void SetErrorState(string? errorMessage, bool canPlayNext = false, bool canPlayPrevious = false);
    void SetActive(bool active);
    void UpdateDuration(TimeSpan duration);
    void SetTrackedDuration(long durationMs);
}
