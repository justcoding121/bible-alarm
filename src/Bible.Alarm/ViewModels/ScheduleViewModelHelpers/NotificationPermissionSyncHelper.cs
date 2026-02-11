#nullable enable

using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Syncs a boolean schedule property with notification permission status.
/// When the value is true but permission is not granted, syncs to false and dispatches the update.
/// </summary>
public static class NotificationPermissionSyncHelper
{
    public static bool SyncValueWithPermission(
        bool value,
        Func<bool> getIsGranted,
        ILogger logger,
        string syncLogMessage,
        string errorLogContext,
        Action dispatchOff)
    {
        if (!value)
        {
            return value;
        }

        try
        {
            if (getIsGranted())
            {
                return true;
            }

            logger.Information(syncLogMessage);
            dispatchOff();
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Context} - assuming not granted", errorLogContext);
            dispatchOff();
            return false;
        }
    }
}
