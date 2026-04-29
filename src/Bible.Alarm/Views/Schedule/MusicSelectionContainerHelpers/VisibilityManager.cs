#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

/// <summary>
/// Manages visibility updates for collapsible content in MusicSelectionContainer.
/// </summary>
public class VisibilityManager
{
    private readonly View collapsibleContent;
    private readonly AnimationManager animationManager;
    private readonly Action? onExpandComplete;

    public VisibilityManager(View collapsibleContent, AnimationManager animationManager, Action? onExpandComplete = null)
    {
        this.collapsibleContent = collapsibleContent;
        this.animationManager = animationManager;
        this.onExpandComplete = onExpandComplete;
    }

    // Track last requested state to prevent duplicate calls
    private bool? lastRequestedState;

    public void UpdateCollapsibleContentVisibility(bool isEnabled, bool animate)
    {
#if DEBUG
        Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.UpdateCollapsibleContentVisibility, isEnabled, animate, collapsibleContent != null, animationManager.IsAnimating);
#endif

        if (collapsibleContent == null)
        {
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.CollapsibleContentNullReturning);
#endif
            return;
        }

        // Prevent duplicate calls with the same state
        if (lastRequestedState == isEnabled && animationManager.IsAnimating)
        {
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.SameStateAlreadyRequestedAnimating);
#endif
            return;
        }

        lastRequestedState = isEnabled;

        if (animate)
        {
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.StartingAnimation);
#endif
            _ = animationManager.AnimateCollapsibleContent(isEnabled, (enabled, cancelled) =>
            {
                if (enabled && !cancelled)
                {
                    onExpandComplete?.Invoke();
                }
            });
        }
        else
        {
            // Set initial state without animation
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.SettingInitialStateWithoutAnimation);
#endif
            animationManager.SetContentStateDirectly(isEnabled);

            // If expanding (enabling), trigger scroll callback after a delay to allow layout to complete
            if (isEnabled && onExpandComplete != null)
            {
                // Use a small delay to ensure layout is complete before scrolling
                collapsibleContent.Dispatcher.DispatchAsync(async () =>
                {
                    await Task.Delay(200); // Delay to allow UI to update
                    onExpandComplete.Invoke();
                });
            }
        }
    }
}

