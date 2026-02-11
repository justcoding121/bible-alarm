#nullable enable

using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles the NotificationEnabled (or IsEnabled on iOS) toggle when user turns it ON and permission is not granted.
/// Returns true if the handler processed the toggle (caller should return early); false if caller should proceed with normal update.
/// </summary>
public static class NotificationEnabledToggleHandler
{
    public static bool TryHandleToggleOnWhenNotGranted(
        bool value,
        Func<bool> getIsGranted,
        Func<bool> requestPermission,
        Action setOffAndNotify,
        Action setOnAndNotify,
        Action<bool> setIsWaitingForPermissionResponse,
        ILogger logger,
        string logContext)
    {
        if (!value)
        {
            return false;
        }

        var isGranted = false;
        try
        {
            isGranted = getIsGranted();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Context} - assuming not granted", logContext);
            setOffAndNotify();
            setIsWaitingForPermissionResponse(true);
            return true;
        }

        if (isGranted)
        {
            return false;
        }

        logger.Debug("Permission not granted - setting toggle to OFF and requesting permission");
        setIsWaitingForPermissionResponse(true);
        setOffAndNotify();

        var requestInitiated = requestPermission();
        if (requestInitiated)
        {
            logger.Debug("Permission already granted after check - allowing toggle ON");
            setIsWaitingForPermissionResponse(false);
            setOnAndNotify();
        }

        return true;
    }
}
