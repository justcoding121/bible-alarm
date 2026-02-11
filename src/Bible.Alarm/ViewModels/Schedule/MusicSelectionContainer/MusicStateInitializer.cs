#nullable enable
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

/// <summary>
/// Handles initialization from state and container ready signaling.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicStateInitializer
{
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly MusicDisplayTextProvider displayTextProvider;
    private readonly MusicPropertyNotifier propertyNotifier;

    private bool hasSignaledReady;
    private bool isReadyActionQueued;

    public MusicStateInitializer(
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        MusicDisplayTextProvider displayTextProvider,
        MusicPropertyNotifier propertyNotifier)
    {
        this.state = state;
        this.dispatcher = dispatcher;
        this.displayTextProvider = displayTextProvider;
        this.propertyNotifier = propertyNotifier;
    }

    public (int scheduleId, bool isNewSchedule, bool? initialMusicEnabled) InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return (0, true, null);
        }

        var scheduleId = currentSchedule.Id;
        var isNewSchedule = currentSchedule.Id <= 0;
        var initialMusicEnabled = currentSchedule.MusicEnabled;

        if (!string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
            !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
        {
            if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
            {
                displayTextProvider.UpdateTrackCache(
                    currentSchedule.MusicTrackName,
                    currentSchedule.MusicTrackCode,
                    currentSchedule.MusicPublicationCode,
                    currentSchedule.MusicLanguageCode);
            }
        }

        propertyNotifier.NotifyAllMusicPropertiesChanged();
        SignalContainerReady();

        return (scheduleId, isNewSchedule, initialMusicEnabled);
    }

    public void SignalContainerReady()
    {
        if (hasSignaledReady || state.Value.ContainerReadiness.MusicSelection) return;
        if (isReadyActionQueued) return;

        isReadyActionQueued = true;
        hasSignaledReady = true;

        if (state.Value.ContainerReadiness.MusicSelection)
        {
            isReadyActionQueued = false;
            hasSignaledReady = true;
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            isReadyActionQueued = false;

            if (state.Value.ContainerReadiness.MusicSelection)
            {
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("MusicSelection"));
        });
    }

    public void ResetReadyFlags()
    {
        hasSignaledReady = false;
        isReadyActionQueued = false;
    }

    public bool ShouldReinitialize()
    {
        return hasSignaledReady && !state.Value.ContainerReadiness.MusicSelection && state.Value.CurrentSchedule != null;
    }

    public bool HasSignaledReady => hasSignaledReady;
}
