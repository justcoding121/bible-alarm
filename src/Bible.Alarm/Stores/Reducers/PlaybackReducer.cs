using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;

namespace Bible.Alarm.Stores.Reducers;

public static class PlaybackReducer
{
    [ReducerMethod]
    public static PlaybackState OnPlaybackStarted(PlaybackState state, PlaybackStartedAction action)
    {
        return new PlaybackState(
            currentScheduleId: action.ScheduleId,
            isPreparingOrPlaying: true,
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: state.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            currentPosition: state.CurrentPosition,
            duration: state.Duration,
            loadedTracks: state.LoadedTracks,
            totalTracks: state.TotalTracks);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackStopped(PlaybackState state, PlaybackStoppedAction action)
    {
        return new PlaybackState(
            currentScheduleId: null,
            isPreparingOrPlaying: false,
            canPlayNext: false,
            canPlayPrevious: false,
            status: PlayStatus.Stopped,
            title: null,
            artist: null,
            album: null,
            artworkUrl: null,
            currentPosition: null,
            duration: TimeSpan.Zero,
            loadedTracks: 0,
            totalTracks: 0);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackNavigationChanged(PlaybackState state, PlaybackNavigationChangedAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            isPreparingOrPlaying: state.IsPreparingOrPlaying,
            canPlayNext: action.CanPlayNext,
            canPlayPrevious: action.CanPlayPrevious,
            status: state.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            currentPosition: state.CurrentPosition,
            duration: state.Duration,
            loadedTracks: state.LoadedTracks,
            totalTracks: state.TotalTracks);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackStatusChanged(PlaybackState state, PlaybackStatusChangedAction action)
    {
        // Update IsPreparingOrPlaying based on status
        var isPreparingOrPlaying = action.Status == PlayStatus.Loading ||
                                   action.Status == PlayStatus.Playing ||
                                   action.Status == PlayStatus.Paused;

        // If status is Stopped/Ended/Failed and we're not preparing/playing, clear schedule
        var currentScheduleId = (isPreparingOrPlaying || state.IsPreparingOrPlaying) 
            ? state.CurrentScheduleId 
            : null;

        return new PlaybackState(
            currentScheduleId: currentScheduleId,
            isPreparingOrPlaying: isPreparingOrPlaying,
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: action.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            currentPosition: state.CurrentPosition,
            duration: state.Duration,
            loadedTracks: state.LoadedTracks,
            totalTracks: state.TotalTracks);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackMetadataChanged(PlaybackState state, PlaybackMetadataChangedAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            isPreparingOrPlaying: state.IsPreparingOrPlaying,
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: state.Status,
            title: action.Title,
            artist: action.Artist,
            album: action.Album,
            artworkUrl: action.ArtworkUrl,
            currentPosition: state.CurrentPosition,
            duration: state.Duration,
            loadedTracks: state.LoadedTracks,
            totalTracks: state.TotalTracks);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackPositionChanged(PlaybackState state, PlaybackPositionChangedAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            isPreparingOrPlaying: state.IsPreparingOrPlaying,
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: state.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            currentPosition: action.CurrentPosition,
            duration: action.Duration,
            loadedTracks: state.LoadedTracks,
            totalTracks: state.TotalTracks);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackPreparationProgress(PlaybackState state, PlaybackPreparationProgressAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            isPreparingOrPlaying: state.IsPreparingOrPlaying,
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: state.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            currentPosition: state.CurrentPosition,
            duration: state.Duration,
            loadedTracks: action.LoadedTracks,
            totalTracks: action.TotalTracks);
    }
}

