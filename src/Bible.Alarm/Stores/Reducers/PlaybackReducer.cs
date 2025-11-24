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
            canPlayPrevious: state.CanPlayPrevious);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackStopped(PlaybackState state, PlaybackStoppedAction action)
    {
        return new PlaybackState(
            currentScheduleId: null,
            isPreparingOrPlaying: false,
            canPlayNext: false,
            canPlayPrevious: false);
    }

    [ReducerMethod]
    public static PlaybackState OnPlaybackNavigationChanged(PlaybackState state, PlaybackNavigationChangedAction action)
    {
        return new PlaybackState(
            currentScheduleId: state.CurrentScheduleId,
            isPreparingOrPlaying: state.IsPreparingOrPlaying,
            canPlayNext: action.CanPlayNext,
            canPlayPrevious: action.CanPlayPrevious);
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
            canPlayPrevious: state.CanPlayPrevious);
    }
}

