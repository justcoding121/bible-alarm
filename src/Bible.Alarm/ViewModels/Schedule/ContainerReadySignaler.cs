#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;

namespace Bible.Alarm.ViewModels.Schedule;

internal sealed class ContainerReadySignaler
{
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly string containerKey;
    private readonly Func<ApplicationState, bool> isReady;

    private bool hasSignaledReady;
    private bool isReadyActionQueued;

    public ContainerReadySignaler(
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        string containerKey,
        Func<ApplicationState, bool> isReady)
    {
        this.state = state;
        this.dispatcher = dispatcher;
        this.containerKey = containerKey;
        this.isReady = isReady;
    }

    public bool HasSignaledReady => hasSignaledReady;

    public void Reset()
    {
        hasSignaledReady = false;
        isReadyActionQueued = false;
    }

    public void TrySignalReady()
    {
        // Check if already signaled or already marked ready in state
        // This check must happen first to prevent any duplicate work
        if (hasSignaledReady || isReady(state.Value))
        {
            return;
        }

        // Check if action is already queued to prevent duplicate queued actions
        // This prevents multiple rapid calls from queuing multiple actions
        if (isReadyActionQueued)
        {
            return;
        }

        // Atomically set both flags to prevent race conditions
        // If another thread/call checks between these lines, it will see isReadyActionQueued=true
        isReadyActionQueued = true;
        hasSignaledReady = true;

        // Double-check state immediately after setting flags (before queuing)
        // This catches the case where state changed between the initial check and flag setting
        if (isReady(state.Value))
        {
            // State already shows ready, reset queued flag and return
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
            if (isReady(state.Value))
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }

            dispatcher.Dispatch(new ContainerReadyAction(containerKey));
        });
    }
}

