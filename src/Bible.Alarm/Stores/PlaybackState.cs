#nullable enable
using Bible.Alarm.Services.Media.Models;
using Fluxor;

namespace Bible.Alarm.Stores;

[FeatureState]
public class PlaybackState
{
    public int? CurrentScheduleId { get; init; }
    public bool IsPreparingOrPlaying { get; init; }
    public bool CanPlayNext { get; init; }
    public bool CanPlayPrevious { get; init; }
    
    // Playback status
    public PlayStatus Status { get; init; }
    
    // Metadata
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
    
    // Duration (updated when track changes, infrequent)
    // Note: CurrentPosition and PreparationProgress are handled via MVVM messaging for performance (high-frequency updates)
    public TimeSpan Duration { get; init; }
    
    // Error message (shown when playback fails)
    public string? ErrorMessage { get; init; }

    public PlaybackState()
    {
        CurrentScheduleId = null;
        IsPreparingOrPlaying = false;
        CanPlayNext = false;
        CanPlayPrevious = false;
        Status = PlayStatus.Stopped;
        Title = null;
        Artist = null;
        Album = null;
        ArtworkUrl = null;
        Duration = TimeSpan.Zero;
        ErrorMessage = null;
    }

    public PlaybackState(
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
        CurrentScheduleId = currentScheduleId;
        IsPreparingOrPlaying = isPreparingOrPlaying;
        CanPlayNext = canPlayNext;
        CanPlayPrevious = canPlayPrevious;
        Status = status;
        Title = title;
        Artist = artist;
        Album = album;
        ArtworkUrl = artworkUrl;
        Duration = duration;
        ErrorMessage = errorMessage;
    }
}

