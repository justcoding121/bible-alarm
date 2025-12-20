#nullable enable
using Bible.Alarm.Services.Media.Models;
using Fluxor;

namespace Bible.Alarm.Stores;

[FeatureState]
public sealed class PlaybackState(
    int? currentScheduleId,
    bool isPreparingOrPlaying,
    bool canPlayNext,
    bool canPlayPrevious,
    PlayStatus status = PlayStatus.Stopped,
    string? title = null,
    string? artist = null,
    string? album = null,
    string? artworkUrl = null,
    TimeSpan duration = default,
    string? errorMessage = null)
{
    public int? CurrentScheduleId { get; init; } = currentScheduleId;
    public bool IsPreparingOrPlaying { get; init; } = isPreparingOrPlaying;
    public bool CanPlayNext { get; init; } = canPlayNext;
    public bool CanPlayPrevious { get; init; } = canPlayPrevious;

    // Playback status
    public PlayStatus Status { get; init; } = status;

    // Metadata
    public string? Title { get; init; } = title;
    public string? Artist { get; init; } = artist;
    public string? Album { get; init; } = album;
    public string? ArtworkUrl { get; init; } = artworkUrl;

    // Duration (updated when track changes, infrequent)
    // Note: CurrentPosition and PreparationProgress are handled via MVVM messaging for performance (high-frequency updates)
    public TimeSpan Duration { get; init; } = duration;

    // Error message (shown when playback fails)
    public string? ErrorMessage { get; init; } = errorMessage;

    public PlaybackState() : this(null, false, false, false, PlayStatus.Stopped, null, null, null, null, TimeSpan.Zero, null)
    {
    }
}

