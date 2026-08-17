#nullable enable
using Bible.Alarm.Services.Media.Models;
using Fluxor;

namespace Bible.Alarm.Stores;

public sealed record PlaybackTransportSlice(
    int? CurrentScheduleId,
    bool IsPreparingOrPlaying,
    bool CanPlayNext,
    bool CanPlayPrevious,
    PlayStatus Status,
    bool IsAutoAdvancing,
    bool IsTransitioningTrack);

public sealed record PlaybackMediaSlice(
    string? Title,
    string? Artist,
    string? Album,
    string? ArtworkUrl,
    TimeSpan Duration,
    string? ErrorMessage);

public sealed record PlaybackDefaultScheduleSlice(
    int? DefaultScheduleId,
    string? DefaultScheduleTitle,
    string? DefaultScheduleArtist,
    string? DefaultScheduleAlbum,
    string? DefaultScheduleArtworkUrl);

/// <summary>
/// Fluxor feature state for playback UI / Android Auto metadata.
/// Transport, media, and default-schedule slices group related fields for construction while exposing a flat property surface.
/// </summary>
[FeatureState]
public sealed class PlaybackState
{
    public int? CurrentScheduleId { get; init; }
    public bool IsPreparingOrPlaying { get; init; }
    public bool CanPlayNext { get; init; }
    public bool CanPlayPrevious { get; init; }

    public PlayStatus Status { get; init; }

    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }

    public TimeSpan Duration { get; init; }

    public string? ErrorMessage { get; init; }

    public int? DefaultScheduleId { get; init; }
    public string? DefaultScheduleTitle { get; init; }
    public string? DefaultScheduleArtist { get; init; }
    public string? DefaultScheduleAlbum { get; init; }
    public string? DefaultScheduleArtworkUrl { get; init; }

    public bool IsAutoAdvancing { get; init; }

    /// <summary>True when Next/Previous was pressed and we are fetching/preparing the new track. Enables immediate progress bar animation.</summary>
    public bool IsTransitioningTrack { get; init; }

    public PlaybackState(PlaybackTransportSlice transport, PlaybackMediaSlice media, PlaybackDefaultScheduleSlice defaults)
    {
        CurrentScheduleId = transport.CurrentScheduleId;
        IsPreparingOrPlaying = transport.IsPreparingOrPlaying;
        CanPlayNext = transport.CanPlayNext;
        CanPlayPrevious = transport.CanPlayPrevious;
        Status = transport.Status;
        IsAutoAdvancing = transport.IsAutoAdvancing;
        IsTransitioningTrack = transport.IsTransitioningTrack;

        Title = media.Title;
        Artist = media.Artist;
        Album = media.Album;
        ArtworkUrl = media.ArtworkUrl;
        Duration = media.Duration;
        ErrorMessage = media.ErrorMessage;

        DefaultScheduleId = defaults.DefaultScheduleId;
        DefaultScheduleTitle = defaults.DefaultScheduleTitle;
        DefaultScheduleArtist = defaults.DefaultScheduleArtist;
        DefaultScheduleAlbum = defaults.DefaultScheduleAlbum;
        DefaultScheduleArtworkUrl = defaults.DefaultScheduleArtworkUrl;
    }

    public PlaybackState()
        : this(
            new PlaybackTransportSlice(null, false, false, false, PlayStatus.Stopped, false, false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null))
    {
    }
}
