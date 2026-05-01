#nullable enable

#if IOS

using Bible.Alarm.Platforms.iOS.Services.Helpers;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.AlarmSettingsContainer;

/// <summary>
/// Inputs for iOS reminder enable guard when toggling IsEnabled while permission state is uncertain.
/// </summary>
public sealed record IosReminderToggleGuardRequest(
    bool Value,
    bool IsUpdatingFromPermissionCheck,
    bool IsSyncingFromState,
    Func<bool> GetIsEnabled,
    IOSNotificationPermissionService? PermissionService,
    ILogger Logger,
    Action SetOnAndNotify,
    Action SetOffAndNotify,
    Action<bool> SetIsUpdatingFromPermissionCheck,
    Action<bool> SetIsWaitingForPermissionResponse);

/// <summary>
/// Handles iOS IsEnabled (reminder) toggle when permission may not be granted.
/// On iOS, reminders require notification permission; there is no separate "Tap to Play" toggle.
/// </summary>
public static class IsEnabledIosPermissionChecker
{
    public static bool TryHandleToggleOnWhenNotGranted(IosReminderToggleGuardRequest r)
    {
        if (!r.Value || r.IsUpdatingFromPermissionCheck || r.IsSyncingFromState)
        {
            return false;
        }

        var isGranted = false;
        try
        {
            if (r.PermissionService != null)
            {
                r.PermissionService.InvalidateCache();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await IOSNotificationPermissionService.IsGrantedAsync();
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (result && !r.GetIsEnabled())
                            {
                                r.SetIsUpdatingFromPermissionCheck(true);
                                try
                                {
                                    r.SetOnAndNotify();
                                    r.Logger.Information("IsEnabled setter (iOS): Permission granted - enabling reminder");
                                }
                                finally
                                {
                                    r.SetIsUpdatingFromPermissionCheck(false);
                                }
                            }
                        });
                    }
                    catch (Exception asyncEx)
                    {
                        r.Logger.Error(asyncEx, "IsEnabled setter (iOS): Exception in async permission check");
                    }
                });
                isGranted = r.PermissionService.IsGranted;
            }
        }
        catch (Exception ex)
        {
            r.Logger.Error(ex, "IsEnabled setter (iOS): Exception checking permission - assuming not granted");
        }

        if (!isGranted)
        {
            r.Logger.Debug("Cannot enable reminder on iOS - notification permission not granted; requesting OS prompt");
            r.SetIsWaitingForPermissionResponse(true);
            r.SetOffAndNotify();
            r.PermissionService?.RequestPermissionIfNeeded();
            return true;
        }

        return false;
    }
}

#endif
