#nullable enable

#if IOS

using Bible.Alarm.Platforms.iOS.Services.Helpers;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.AlarmSettingsContainer;

/// <summary>
/// Handles iOS IsEnabled (reminder) toggle when permission may not be granted.
/// On iOS, reminders require notification permission; there is no separate "Tap to Play" toggle.
/// </summary>
public static class IsEnabledIosPermissionChecker
{
    public static bool TryHandleToggleOnWhenNotGranted(
        bool value,
        bool isUpdatingFromPermissionCheck,
        bool isSyncingFromState,
        Func<bool> getIsEnabled,
        IOSNotificationPermissionService? permissionService,
        ILogger logger,
        Action setOnAndNotify,
        Action setOffAndNotify,
        Action<bool> setIsUpdatingFromPermissionCheck,
        Action<bool> setIsWaitingForPermissionResponse)
    {
        if (!value || isUpdatingFromPermissionCheck || isSyncingFromState)
        {
            return false;
        }

        var isGranted = false;
        try
        {
            if (permissionService != null)
            {
                permissionService.InvalidateCache();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await permissionService.IsGrantedAsync();
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (result && !getIsEnabled())
                            {
                                setIsUpdatingFromPermissionCheck(true);
                                try
                                {
                                    setOnAndNotify();
                                    logger.Information("IsEnabled setter (iOS): Permission granted - enabling reminder");
                                }
                                finally
                                {
                                    setIsUpdatingFromPermissionCheck(false);
                                }
                            }
                        });
                    }
                    catch (Exception asyncEx)
                    {
                        logger.Error(asyncEx, "IsEnabled setter (iOS): Exception in async permission check");
                    }
                });
                isGranted = permissionService.IsGranted;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "IsEnabled setter (iOS): Exception checking permission - assuming not granted");
        }

        if (!isGranted)
        {
            logger.Debug("Cannot enable reminder on iOS - notification permission not granted; requesting OS prompt");
            setIsWaitingForPermissionResponse(true);
            setOffAndNotify();
            permissionService?.RequestPermissionIfNeeded();
            return true;
        }

        return false;
    }
}

#endif
