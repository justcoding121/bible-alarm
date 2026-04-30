#nullable enable

using System.ComponentModel;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

/// <summary>
/// Handles property change events from MusicSelectionContainerViewModel.
/// </summary>
public class PropertyChangeHandler : IDisposable
{
    private readonly MusicSelectionContainer container;
    private readonly Action<bool, bool> updateVisibility;
    private readonly Action scrollToBottom;
    private CancellationTokenSource? debounceTokenSource;

    public bool IsInitialLoad { get; set; } = true;

    public bool LastMusicEnabledState { get; set; }

    public PropertyChangeHandler(
        MusicSelectionContainer container,
        Action<bool, bool> updateVisibility,
        Action scrollToBottom)
    {
        this.container = container;
        this.updateVisibility = updateVisibility;
        this.scrollToBottom = scrollToBottom;
    }

    public bool ShouldScrollOnExpand { get; set; }

    public void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e, MusicSelectionContainerViewModel? viewModel)
    {
        if (sender is not MusicSelectionContainerViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.MusicEnabled))
        {
            var newState = vm.MusicEnabled;

#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.PropertyChangedMusicEnabledState, newState, LastMusicEnabledState, IsInitialLoad);
#endif

            // Debounce rapid changes (but not during initial load - always update during initial load)
            if (!IsInitialLoad && newState == LastMusicEnabledState)
            {
#if DEBUG
                Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.StateUnchangedIgnoring);
#endif
                return; // Ignore if state hasn't actually changed (but only after initial load)
            }

            // During initial load, still update visibility but without animation
            // This ensures the UI reflects the correct state even during initialization
            if (IsInitialLoad)
            {
#if DEBUG
                Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.PropertyChangeDuringInitialLoadUpdatingVisibility, newState, LastMusicEnabledState);
#endif
                LastMusicEnabledState = newState; // Update last state
                // Still update visibility, just without animation
                container.Dispatcher.Dispatch(() =>
                {
                    if (container.Handler != null)
                    {
                        updateVisibility(newState, false);
                    }
                });
                return;
            }

            // Only scroll if user is toggling from false to true (user-initiated expand)
            ShouldScrollOnExpand = !LastMusicEnabledState && newState;

            LastMusicEnabledState = newState;

#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.TriggeringAnimationMusicEnabled, newState, ShouldScrollOnExpand);
#endif

            // Cancel and dispose any pending debounce
            debounceTokenSource?.Cancel();
            debounceTokenSource?.Dispose();
            debounceTokenSource = new CancellationTokenSource();
            var token = debounceTokenSource.Token;

            // Use animation for user-initiated changes (not initial load)
            // This provides better UX and ensures scroll callback is triggered
            var shouldAnimate = true;

            // Small delay to debounce rapid changes, but trigger update immediately on UI thread
            container.Dispatcher.Dispatch(() =>
            {
                if (!token.IsCancellationRequested && container.Handler != null)
                {
#if DEBUG
                    Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.CallingUpdateCollapsibleContentVisibility, shouldAnimate, newState);
#endif
                    updateVisibility(newState, shouldAnimate);
                }
            });
        }
        else if (e.PropertyName == nameof(MusicSelectionContainerViewModel.ShouldScrollToBottom) && vm.ShouldScrollToBottom)
        {
            // Scroll to bottom when ViewModel signals it
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ShouldScrollToBottomScrolling);
#endif

            // Small delay to ensure layout is complete
            container.Dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(200); // Delay to allow UI to update
                scrollToBottom();

                // Reset the flag after scrolling
                if (viewModel != null)
                {
                    viewModel.ShouldScrollToBottom = false;
                }
            });
        }
    }

    public void Dispose()
    {
        try
        {
            debounceTokenSource?.Cancel();
            debounceTokenSource?.Dispose();
        }
        catch
        {
            // Ignore errors during cancellation/disposal
        }
    }
}

