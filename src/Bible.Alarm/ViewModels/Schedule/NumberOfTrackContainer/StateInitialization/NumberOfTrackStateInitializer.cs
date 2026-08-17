#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.StateInitialization;

/// <summary>
/// Maps CurrentSchedule fields into NumberOfTrackContainerViewModel init values, syncing notification enablement with OS permission on mobile.
/// </summary>
public static class NumberOfTrackStateInitializer
{
    public static InitResult? TryInitialize(
        ScheduleStateItem? currentSchedule,
        Func<bool> getIsGranted,
        ILogger logger)
    {
        if (currentSchedule == null)
        {
            return null;
        }

        var notificationEnabled = currentSchedule.NotificationEnabled;
#if ANDROID || IOS
        notificationEnabled = NotificationPermissionSyncHelper.SyncValueWithPermission(
            notificationEnabled,
            getIsGranted,
            logger,
            "InitializeFromState: NotificationEnabled is true in state but permission is not granted - setting local property to OFF",
            "InitializeFromState: Exception checking notification permission",
            () => { });
#endif

        return new InitResult(
            currentSchedule.Id,
            notificationEnabled,
            currentSchedule.AlwaysPlayFromStart,
            currentSchedule.NumberOfTracksToPlay <= 0,
            currentSchedule.BiblePublicationCategoryName);
    }
}
