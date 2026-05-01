#nullable enable

using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Arguments for <see cref="NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted"/>.
/// </summary>
public sealed record NotificationEnabledToggleRequest(
    bool Value,
    Func<bool> GetIsGranted,
    Func<bool> RequestPermission,
    Action SetOffAndNotify,
    Action SetOnAndNotify,
    Action<bool> SetIsWaitingForPermissionResponse,
    ILogger Logger,
    string LogContext);

/// <summary>
/// Handles the NotificationEnabled (or IsEnabled on iOS) toggle when user turns it ON and permission is not granted.
/// Returns true if the handler processed the toggle (caller should return early); false if caller should proceed with normal update.
/// </summary>
public static class NotificationEnabledToggleHandler
{
    public static bool TryHandleToggleOnWhenNotGranted(NotificationEnabledToggleRequest r)
    {
        if (!r.Value)
        {
            return false;
        }

        var isGranted = false;
        try
        {
            isGranted = r.GetIsGranted();
        }
        catch (Exception ex)
        {
            r.Logger.Error(ex, "{Context} - assuming not granted", r.LogContext);
            r.SetOffAndNotify();
            r.SetIsWaitingForPermissionResponse(true);
            return true;
        }

        if (isGranted)
        {
            return false;
        }

        r.Logger.Debug("Permission not granted - setting toggle to OFF and requesting permission");
        r.SetIsWaitingForPermissionResponse(true);
        r.SetOffAndNotify();

        var requestInitiated = r.RequestPermission();
        if (requestInitiated)
        {
            r.Logger.Debug("Permission already granted after check - allowing toggle ON");
            r.SetIsWaitingForPermissionResponse(false);
            r.SetOnAndNotify();
        }

        return true;
    }
}
