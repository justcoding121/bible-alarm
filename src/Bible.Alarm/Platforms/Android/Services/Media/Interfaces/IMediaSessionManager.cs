#nullable enable
using Android.Support.V4.Media.Session;
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Platforms.Android.Services.Media.Interfaces;

/// <summary>
/// Manager for the shared MediaSessionCompat instance used by Android Auto and system controls.
/// </summary>
public interface IMediaSessionManager
{
    MediaSessionCompat GetOrCreate(bool isConnect = false);
    void UpdatePlaybackState(int state, long position = 0, bool canPlayNext = false, bool canPlayPrevious = false);
    void UpdatePlaybackPosition(TimeSpan position, TimeSpan duration, bool canPlayNext = false, bool canPlayPrevious = false);
    void UpdateMetadata(string title, string artist, string? album = null, int? scheduleId = null, string? artworkUrl = null);
    void SetBufferingStateOnly();
    void UpdatePlaybackStateForStop();
    void SetPlaybackStatus(PlayStatus status, bool canPlayNext = false, bool canPlayPrevious = false);
    void SetActive(bool active);
    void UpdateDuration(TimeSpan duration);
    void SetTrackedDuration(long durationMs);
    MediaSessionCompat.Token? Token { get; }
}
