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
        var defaults = Defaults(state);
        if (scheduleChanged)
        {
            defaults = defaults with { DefaultScheduleArtworkUrl = null };
        }

        return new PlaybackState(
            new PlaybackTransportSlice(
                action.ScheduleId,
                true,
                true,
                true,
                PlayStatus.Loading,
                state.IsAutoAdvancing,
                state.IsTransitioningTrack),
            new PlaybackMediaSlice(
                state.Title,
                state.Artist,
                state.Album,
                scheduleChanged ? null : state.ArtworkUrl,
                state.Duration,
                null),
            defaults);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackStopped(PlaybackState state, PlaybackStoppedAction action)
    {
        return new PlaybackState(
            new PlaybackTransportSlice(null, false, false, false, PlayStatus.Stopped, false, false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackNavigationChanged(PlaybackState state, PlaybackNavigationChangedAction action)
    {
        return new PlaybackState(
            new PlaybackTransportSlice(
                state.CurrentScheduleId,
                state.IsPreparingOrPlaying,
                action.CanPlayNext,
                action.CanPlayPrevious,
                state.Status,
                state.IsAutoAdvancing,
                state.IsTransitioningTrack),
            Media(state),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackStatusChanged(PlaybackState state, PlaybackStatusChangedAction action)
    {
        var isPreparingOrPlaying = action.Status == PlayStatus.Loading ||
                                   action.Status == PlayStatus.Playing ||
                                   action.Status == PlayStatus.Paused ||
                                   (action.Status == PlayStatus.Failed && state.CurrentScheduleId.HasValue) ||
                                   (action.Status == PlayStatus.Stopped && state.CurrentScheduleId.HasValue);

        var currentScheduleId = (isPreparingOrPlaying || state.IsPreparingOrPlaying)
            ? state.CurrentScheduleId
            : null;

        var wasAutoAdvancing = state.IsAutoAdvancing;
        var isAutoAdvancing = action.Status != PlayStatus.Playing && state.IsAutoAdvancing;

        if (wasAutoAdvancing && !isAutoAdvancing && action.Status == PlayStatus.Playing)
        {
            Serilog.Log.Information(
                AppConstants.Logging.PlaybackReducerDiagnosticsLog.AutoAdvancingFlagClearedWhenPlaying,
                state.Status,
                state.CurrentScheduleId);
        }

        return new PlaybackState(
            new PlaybackTransportSlice(
                currentScheduleId,
                isPreparingOrPlaying,
                state.CanPlayNext,
                state.CanPlayPrevious,
                action.Status,
                isAutoAdvancing,
                state.IsTransitioningTrack),
            Media(state),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackMetadataChanged(PlaybackState state, PlaybackMetadataChangedAction action)
    {
        return new PlaybackState(
            Transport(state),
            new PlaybackMediaSlice(
                action.Title,
                action.Artist,
                action.Album,
                action.ArtworkUrl,
                state.Duration,
                state.ErrorMessage),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackDurationChanged(PlaybackState state, PlaybackDurationChangedAction action)
    {
        return new PlaybackState(
            Transport(state),
            new PlaybackMediaSlice(
                state.Title,
                state.Artist,
                state.Album,
                state.ArtworkUrl,
                action.Duration,
                state.ErrorMessage),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackError(PlaybackState state, PlaybackErrorAction action)
    {
        return new PlaybackState(
            Transport(state),
            new PlaybackMediaSlice(
                state.Title,
                state.Artist,
                state.Album,
                state.ArtworkUrl,
                state.Duration,
                action.ErrorMessage),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnSetDefaultScheduleMetadata(PlaybackState state, SetDefaultScheduleMetadataAction action)
    {
        return new PlaybackState(
            Transport(state),
            Media(state),
            new PlaybackDefaultScheduleSlice(
                action.ScheduleId,
                action.Title,
                action.Artist,
                action.Album,
                action.ArtworkUrl));
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
            new PlaybackTransportSlice(
                state.CurrentScheduleId,
                state.IsPreparingOrPlaying,
                state.CanPlayNext,
                state.CanPlayPrevious,
                state.Status,
                isAutoAdvancing,
                state.IsTransitioningTrack),
            Media(state),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackTrackTransitionStarted(PlaybackState state, PlaybackTrackTransitionStartedAction action)
    {
        return new PlaybackState(
            Transport(state),
            new PlaybackMediaSlice(
                state.Title,
                state.Artist,
                state.Album,
                state.ArtworkUrl,
                TimeSpan.Zero,
                state.ErrorMessage),
            Defaults(state));
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackTrackTransitionEnded(PlaybackState state, PlaybackTrackTransitionEndedAction action)
    {
        return new PlaybackState(
            new PlaybackTransportSlice(
                state.CurrentScheduleId,
                state.IsPreparingOrPlaying,
                state.CanPlayNext,
                state.CanPlayPrevious,
                state.Status,
                state.IsAutoAdvancing,
                false),
            Media(state),
            Defaults(state));
    }

    private static PlaybackTransportSlice Transport(PlaybackState s) =>
        new(s.CurrentScheduleId, s.IsPreparingOrPlaying, s.CanPlayNext, s.CanPlayPrevious, s.Status, s.IsAutoAdvancing, s.IsTransitioningTrack);

    private static PlaybackMediaSlice Media(PlaybackState s) =>
        new(s.Title, s.Artist, s.Album, s.ArtworkUrl, s.Duration, s.ErrorMessage);

    private static PlaybackDefaultScheduleSlice Defaults(PlaybackState s) =>
        new(s.DefaultScheduleId, s.DefaultScheduleTitle, s.DefaultScheduleArtist, s.DefaultScheduleAlbum, s.DefaultScheduleArtworkUrl);
}
