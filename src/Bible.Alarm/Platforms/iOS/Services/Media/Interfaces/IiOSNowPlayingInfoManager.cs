#nullable enable
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;

/// <summary>
/// Manages MPNowPlayingInfoCenter for iOS Now Playing display.
/// </summary>
public interface IiOSNowPlayingInfoManager
{
    void UpdateMetadata(string? title, string? artist, string? album, TimeSpan duration, string? artworkUrl);
    void UpdatePlaybackPosition(TimeSpan currentPosition, TimeSpan duration, PlayStatus status);
    void UpdatePlaybackStatus(PlayStatus status);
    void UpdateDuration(TimeSpan duration);
    void ClearNowPlayingInfo();
    void SetDefaultMetadata(string? title, string? artist, string? album, string? artworkUrl);
}
