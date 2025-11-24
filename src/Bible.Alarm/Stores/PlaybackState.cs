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
    
    // Position and duration
    public TimeSpan? CurrentPosition { get; init; }
    public TimeSpan Duration { get; init; }
    
    // Preparation progress
    public int LoadedTracks { get; init; }
    public int TotalTracks { get; init; }
    public bool IsPreparing => TotalTracks > 0 && LoadedTracks < TotalTracks;
    
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
        CurrentPosition = null;
        Duration = TimeSpan.Zero;
        LoadedTracks = 0;
        TotalTracks = 0;
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
        TimeSpan? currentPosition = null,
        TimeSpan duration = default,
        int loadedTracks = 0,
        int totalTracks = 0,
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
        CurrentPosition = currentPosition;
        Duration = duration;
        LoadedTracks = loadedTracks;
        TotalTracks = totalTracks;
        ErrorMessage = errorMessage;
    }
}

