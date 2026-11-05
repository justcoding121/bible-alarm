#nullable enable

namespace Bible.Alarm.Platforms.Android.Services.Media.Session;

/// <summary>
/// Playback state exposed to the rest of the app. The compat session and the Media3 session
/// both map onto this, so Fluxor effects do not reference Android session types.
/// </summary>
public enum SessionPlaybackState
{
    None = 0,
    Buffering,
    Playing,
    Paused,
    Error,
}

/// <summary>
/// Read-only view of the session. Position is milliseconds.
/// </summary>
public sealed record SessionPlaybackSnapshot(
    SessionPlaybackState State,
    long PositionMs,
    long DurationMs,
    bool CanPlayNext,
    bool CanPlayPrevious,
    string? Title,
    string? Artist,
    string? Album,
    string? ArtworkUrl,
    int? ScheduleId,
    bool IsActive,
    string? ErrorMessage);
