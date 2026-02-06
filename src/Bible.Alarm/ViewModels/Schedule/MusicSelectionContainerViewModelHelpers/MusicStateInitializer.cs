#nullable enable
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

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

    /// <summary>
    /// Initializes from CurrentSchedule state.
    /// </summary>
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

        // Initialize track name cache from state if available (populated during bootstrap)
        // NOTE: Do NOT query database here - track names should be in state from bootstrap
        // Music type is now inferred from LanguageCode: NULL/empty = instrumental, otherwise = vocal
        if (!string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
            !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
        {
            // If MusicTrackName is already in state (from bootstrap), use it immediately
            if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
            {
                displayTextProvider.UpdateTrackCache(
                    currentSchedule.MusicTrackName,
                    currentSchedule.MusicTrackCode,
                    currentSchedule.MusicPublicationCode,
                    currentSchedule.MusicLanguageCode);
            }
        }

        // Batch property notifications to reduce UI thread work
        propertyNotifier.NotifyAllMusicPropertiesChanged();

        // Signal that this container is ready (initialized from CurrentSchedule)
        SignalContainerReady();

        return (scheduleId, isNewSchedule, initialMusicEnabled);
    }

    /// <summary>
    /// Signals that this container is ready.
    /// </summary>
    public void SignalContainerReady()
    {
        // Check if already signaled or already marked ready in state
        // This check must happen first to prevent any duplicate work
        if (hasSignaledReady || state.Value.ContainerReadiness.MusicSelection) return;

        // Check if action is already queued to prevent duplicate queued actions
        // This prevents multiple rapid calls from queuing multiple actions
        if (isReadyActionQueued) return;

        // Atomically set both flags to prevent race conditions
        // If another thread/call checks between these lines, it will see isReadyActionQueued=true
        isReadyActionQueued = true;
        hasSignaledReady = true;

        // Double-check state immediately after setting flags (before queuing)
        // This catches the case where state changed between the initial check and flag setting
        if (state.Value.ContainerReadiness.MusicSelection)
        {
            // State already shows ready, reset flags and return
            isReadyActionQueued = false;
            hasSignaledReady = true;
            return;
        }

        // Dispatch to state that this container is ready
        // Check state again inside the queued action to prevent duplicates from queued actions
        MainThread.BeginInvokeOnMainThread(() =>
        {
            isReadyActionQueued = false; // Reset flag when action executes

            // Final check before dispatching - if state already shows we're ready, another action already handled it
            if (state.Value.ContainerReadiness.MusicSelection)
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("MusicSelection"));
        });
    }

    /// <summary>
    /// Resets the ready flags (called when a new schedule is opened).
    /// </summary>
    public void ResetReadyFlags()
    {
        hasSignaledReady = false;
        isReadyActionQueued = false;
    }

    /// <summary>
    /// Checks if container readiness was reset and needs re-initialization.
    /// </summary>
    public bool ShouldReinitialize()
    {
        return hasSignaledReady && !state.Value.ContainerReadiness.MusicSelection && state.Value.CurrentSchedule != null;
    }

    /// <summary>
    /// Checks if container has signaled ready.
    /// </summary>
    public bool HasSignaledReady => hasSignaledReady;
}
