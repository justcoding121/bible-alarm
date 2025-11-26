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
            duration: state.Duration,
            errorMessage: null); // Clear error when starting new playback
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
            duration: TimeSpan.Zero,
            errorMessage: null);
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
            duration: state.Duration,
            errorMessage: state.ErrorMessage);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackStatusChanged(PlaybackState state, PlaybackStatusChangedAction action)
    {
        // Update IsPreparingOrPlaying based on status
        // Keep modal open when:
        // - Loading, Playing, or Paused (normal playback states)
        // - Failed (to show error message) if we have a current schedule
        // - Stopped (when switching tracks) if we have a current schedule (keep modal open during track navigation)
        var isPreparingOrPlaying = action.Status == PlayStatus.Loading ||
                                   action.Status == PlayStatus.Playing ||
                                   action.Status == PlayStatus.Paused ||
                                   (action.Status == PlayStatus.Failed && state.CurrentScheduleId.HasValue) ||
                                   (action.Status == PlayStatus.Stopped && state.CurrentScheduleId.HasValue);

        // If status is Stopped/Ended and we're not preparing/playing, clear schedule
        // Keep schedule when Failed or Stopped (during track navigation) to allow error display and retry
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
            duration: state.Duration,
            errorMessage: state.ErrorMessage);
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
            duration: state.Duration,
            errorMessage: state.ErrorMessage);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackDurationChanged(PlaybackState state, PlaybackDurationChangedAction action)
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
            duration: action.Duration,
            errorMessage: state.ErrorMessage);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackError(PlaybackState state, PlaybackErrorAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            isPreparingOrPlaying: state.IsPreparingOrPlaying, // Keep modal open to show error
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: state.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            duration: state.Duration,
            errorMessage: action.ErrorMessage);
    }
}

