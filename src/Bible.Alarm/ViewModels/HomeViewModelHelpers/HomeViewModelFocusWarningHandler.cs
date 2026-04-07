#nullable enable
using Fluxor;
using Bible.Alarm.Stores;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles iOS Focus/Sleep warning button visibility for HomeViewModel.
/// Shows a warning when the user has enabled schedules but hasn't acknowledged
/// the Focus/Sleep settings guidance (iOS only).
/// </summary>
public sealed class HomeViewModelFocusWarningHandler
{
    private static readonly string FocusWarningDismissedKey = "ios_focus_warning_dismissed";

    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;

    public HomeViewModelFocusWarningHandler(ILogger logger, IState<ApplicationState> state)
    {
        this.logger = logger;
        this.state = state;
    }

    public bool ComputeShouldShow()
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS)
        {
            return false;
        }

        try
        {
            var hasEnabledSchedules = state.Value.Schedules?.Any(s => s.IsEnabled) ?? false;
            var isDismissed = Preferences.Get(FocusWarningDismissedKey, false);

            var shouldShow = hasEnabledSchedules && !isDismissed;
            logger.Debug("FocusWarningHandler: HasEnabledSchedules={HasEnabled}, Dismissed={Dismissed}, ShouldShow={ShouldShow}",
                hasEnabledSchedules, isDismissed, shouldShow);
            return shouldShow;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error computing Focus warning visibility");
            return false;
        }
    }

    public static void Dismiss()
    {
        Preferences.Set(FocusWarningDismissedKey, true);
    }
}
