#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles notification permission button visibility for HomeViewModel.
/// </summary>
public sealed class HomeViewModelNotificationPermissionHandler
{
    private readonly ILogger logger;
    [SuppressMessage("SonarAnalyzer.CSharp", "S4487", Justification = "Used only in ANDROID/IOS preprocessor blocks; unreachable on other targets.")]
    private readonly IState<ApplicationState> state;

    public HomeViewModelNotificationPermissionHandler(ILogger logger, IState<ApplicationState> state)
    {
        this.logger = logger;
        this.state = state;
    }

    public void UpdateVisibility(
        Action<bool> setButtonVisible,
        Action<double> setButtonBottomMargin,
        Action<double> setCollectionViewBottomMargin,
        Func<bool> getFloatingButtonVisible,
        Func<bool> getNotificationButtonVisible,
        Action notifyMarginChanged)
    {
        try
        {
            logger.Information("UpdateNotificationPermissionButtonVisibility called - Platform={Platform}", DeviceInfo.Platform);

#if ANDROID
            var shouldShow = ComputeAndroidShouldShow();
#elif IOS
            var shouldShow = ComputeIosShouldShow(setButtonVisible, setButtonBottomMargin, setCollectionViewBottomMargin, notifyMarginChanged);
#else
            var shouldShow = false;
#endif

            logger.Information("[NOTIFICATION-BUTTON] Current button visible: {CurrentVisible}, Should show: {ShouldShow}",
                getNotificationButtonVisible(), shouldShow);

            setButtonVisible(shouldShow);

#if ANDROID
            if (shouldShow || getFloatingButtonVisible())
            {
                setButtonBottomMargin(0);
                setCollectionViewBottomMargin(24 + 56);
            }
            else
            {
                setButtonBottomMargin(0);
                setCollectionViewBottomMargin(0);
            }
#endif

            MainThread.BeginInvokeOnMainThread(notifyMarginChanged);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating notification permission button visibility");
            setButtonVisible(false);
            setButtonBottomMargin(0);
        }
    }

#if ANDROID
    private bool ComputeAndroidShouldShow()
    {
        var isGranted = GetAndroidPermissionGranted();
        var hasRelevant = state.Value.Schedules?.Any(s => s.NotificationEnabled && s.IsEnabled) ?? false;
        var shouldShow = !isGranted && hasRelevant;
        logger.Debug("UpdateNotificationPermissionButtonVisibility (Android): PermissionGranted={PermissionGranted}, HasNotificationEnabledSchedule={HasSchedule}, ShouldShow={ShouldShow}",
            isGranted, hasRelevant, shouldShow);
        return shouldShow;
    }

    private bool GetAndroidPermissionGranted()
    {
        try
        {
            return Platforms.Android.Services.Helpers.NotificationPermissionService.Instance.IsGranted;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception checking notification permission - assuming not granted");
            return false;
        }
    }
#elif IOS
    private bool ComputeIosShouldShow(
        Action<bool> setButtonVisible,
        Action<double> setButtonBottomMargin,
        Action<double> setCollectionViewBottomMargin,
        Action notifyMarginChanged)
    {
        logger.Information("[NOTIFICATION-BUTTON] UpdateNotificationPermissionButtonVisibility (iOS): Starting");

        var isGranted = GetIosPermissionGranted(setButtonVisible, setButtonBottomMargin, setCollectionViewBottomMargin, notifyMarginChanged);
        var hasReminderEnabled = state.Value.Schedules?.Any(s => s.IsEnabled) ?? false;
        var shouldShow = !isGranted && hasReminderEnabled;

        logger.Debug("UpdateNotificationPermissionButtonVisibility (iOS): PermissionGranted={PermissionGranted}, HasReminderEnabled={HasReminder}, ShouldShow={ShouldShow}",
            isGranted, hasReminderEnabled, shouldShow);

        setButtonBottomMargin(shouldShow ? 24 : 0);
        setCollectionViewBottomMargin(shouldShow ? 24 + 56 : 0);
        return shouldShow;
    }

    private bool GetIosPermissionGranted(
        Action<bool> setButtonVisible,
        Action<double> setButtonBottomMargin,
        Action<double> setCollectionViewBottomMargin,
        Action notifyMarginChanged)
    {
        try
        {
            var permissionService = Platforms.iOS.Services.Helpers.IosNotificationPermissionService.Instance;
            var isGranted = permissionService.IsGranted;
            logger.Information("[NOTIFICATION-BUTTON] Permission granted: {PermissionGranted}", isGranted);

            _ = Task.Run(async () =>
            {
                try
                {
                    var asyncResult = await Platforms.iOS.Services.Helpers.IosNotificationPermissionService.IsGrantedAsync();
                    logger.Information("[NOTIFICATION-BUTTON] Async permission check completed: {Result}", asyncResult);
                    if (asyncResult != isGranted)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                            ApplyIosAsyncPermissionMargins(
                                logger,
                                state,
                                asyncResult,
                                setButtonVisible,
                                setButtonBottomMargin,
                                setCollectionViewBottomMargin,
                                notifyMarginChanged));
                    }
                }
                catch (Exception asyncEx)
                {
                    logger.Error(asyncEx, "[NOTIFICATION-BUTTON] Error in async permission check");
                }
            });

            return isGranted;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[NOTIFICATION-BUTTON] Exception checking notification permission");
            return false;
        }
    }

    private static void ApplyIosAsyncPermissionMargins(
        ILogger logger,
        IState<ApplicationState> state,
        bool asyncPermissionGranted,
        Action<bool> setButtonVisible,
        Action<double> setButtonBottomMargin,
        Action<double> setCollectionViewBottomMargin,
        Action notifyMarginChanged)
    {
        try
        {
            var hasReminderEnabled = state.Value.Schedules?.Any(s => s.IsEnabled) ?? false;
            var shouldShowAsync = !asyncPermissionGranted && hasReminderEnabled;
            setButtonVisible(shouldShowAsync);
            setButtonBottomMargin(shouldShowAsync ? 24 : 0);
            setCollectionViewBottomMargin(shouldShowAsync ? 24 + 56 : 0);
            notifyMarginChanged();
            logger.Information("[NOTIFICATION-BUTTON] Updated button visibility from async check: {ShouldShow}", shouldShowAsync);
        }
        catch (Exception uiEx)
        {
            logger.Error(uiEx, "[NOTIFICATION-BUTTON] Error updating UI from async result");
        }
    }
#endif
}
