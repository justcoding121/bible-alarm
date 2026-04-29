using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;

namespace Bible.Alarm.Stores.Reducers;

public static class PlaybackReducer
{
    [ReducerMethod]
    public static PlaybackState OnPlaybackStarted(PlaybackState state, PlaybackStartedAction action)
    {
        var scheduleChanged = state.CurrentScheduleId != action.ScheduleId;
        return new PlaybackState(
            currentScheduleId: action.ScheduleId,
            isPreparingOrPlaying: true,
            // Next/Previous are always enabled during playback sessions.
            canPlayNext: true,
            canPlayPrevious: true,
            status: PlayStatus.Loading,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: scheduleChanged ? null : state.ArtworkUrl,
            duration: state.Duration,
            // Clear error when starting new playback
            errorMessage: null,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: scheduleChanged ? null : state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
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
            errorMessage: null,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: false,
            isTransitioningTrack: false);
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
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
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

        // Clear auto-advancing flag when playback actually starts (Playing status)
        // This ensures smooth transition: auto-advancing keeps pause button visible during Loading/Stopped,
        // but once Playing, we use the actual status
        // Keep flag set during Loading/Stopped transitions to prevent flicker
        var wasAutoAdvancing = state.IsAutoAdvancing;
        var isAutoAdvancing = action.Status == PlayStatus.Playing
            ? false
            : state.IsAutoAdvancing;

        if (wasAutoAdvancing && !isAutoAdvancing && action.Status == PlayStatus.Playing)
        {
            Serilog.Log.Information(
                AppConstants.Logging.PlaybackReducerDiagnosticsLog.AutoAdvancingFlagClearedWhenPlaying,
                state.Status,
                state.CurrentScheduleId);
        }

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
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: isAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
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
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
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
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackError(PlaybackState state, PlaybackErrorAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            // Keep modal open to show error
            isPreparingOrPlaying: state.IsPreparingOrPlaying,
            canPlayNext: state.CanPlayNext,
            canPlayPrevious: state.CanPlayPrevious,
            status: state.Status,
            title: state.Title,
            artist: state.Artist,
            album: state.Album,
            artworkUrl: state.ArtworkUrl,
            duration: state.Duration,
            errorMessage: action.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
    }

    [ReducerMethod]
    public static PlaybackState OnSetDefaultScheduleMetadata(PlaybackState state, SetDefaultScheduleMetadataAction action)
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
            duration: state.Duration,
            errorMessage: state.ErrorMessage,
            defaultScheduleId: action.ScheduleId,
            defaultScheduleTitle: action.Title,
            defaultScheduleArtist: action.Artist,
            defaultScheduleAlbum: action.Album,
            defaultScheduleArtworkUrl: action.ArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
    }

    [ReducerMethod]
    public static PlaybackState OnSetAutoAdvancing(PlaybackState state, SetAutoAdvancingAction action)
    {
        var wasAutoAdvancing = state.IsAutoAdvancing;
        var isAutoAdvancing = action.IsAutoAdvancing;

        if (wasAutoAdvancing != isAutoAdvancing)
        {
            Serilog.Log.Information(
                AppConstants.Logging.PlaybackReducerDiagnosticsLog.AutoAdvancingFlagChanged,
                wasAutoAdvancing,
                isAutoAdvancing,
                state.Status,
                state.CurrentScheduleId);
        }

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
            duration: state.Duration,
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: isAutoAdvancing,
            isTransitioningTrack: state.IsTransitioningTrack);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackTrackTransitionStarted(PlaybackState state, PlaybackTrackTransitionStartedAction action)
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
            duration: TimeSpan.Zero,
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: true);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackTrackTransitionEnded(PlaybackState state, PlaybackTrackTransitionEndedAction action)
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
            duration: state.Duration,
            errorMessage: state.ErrorMessage,
            defaultScheduleId: state.DefaultScheduleId,
            defaultScheduleTitle: state.DefaultScheduleTitle,
            defaultScheduleArtist: state.DefaultScheduleArtist,
            defaultScheduleAlbum: state.DefaultScheduleAlbum,
            defaultScheduleArtworkUrl: state.DefaultScheduleArtworkUrl,
            isAutoAdvancing: state.IsAutoAdvancing,
            isTransitioningTrack: false);
    }
}

