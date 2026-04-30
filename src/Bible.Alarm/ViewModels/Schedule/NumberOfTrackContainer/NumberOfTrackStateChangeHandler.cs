#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

/// <summary>
/// Handles property sync from ApplicationState when schedule changes within the same schedule.
/// Extracted from NumberOfTrackContainerViewModel.OnStateChanged.
/// </summary>
public static class NumberOfTrackStateChangeHandler
{
    public static void ApplyPropertyChanges(
        ScheduleStateItem currentSchedule,
        ref bool notificationEnabled,
        ref bool alwaysPlayFromStart,
        ref bool playIndefinitely,
        ref string? lastCategoryName,
        bool isWaitingForPermissionResponse,
        Func<bool> getIsGranted,
        Action dispatchNotificationEnabledOff,
        Func<int, Task> populateListViewAsync,
        Action dispatchNumberOfTracksToPlay,
        ILogger logger)
    {
        if (!isWaitingForPermissionResponse && notificationEnabled != currentSchedule.NotificationEnabled)
        {
            var newValue = currentSchedule.NotificationEnabled;
#if ANDROID || IOS
            newValue = NotificationPermissionSyncHelper.SyncValueWithPermission(
                newValue,
                getIsGranted,
                logger,
                "OnStateChanged: NotificationEnabled is true in state but permission is not granted - syncing to OFF",
                "OnStateChanged: Exception checking notification permission",
                dispatchNotificationEnabledOff);
#endif
            notificationEnabled = newValue;
        }

        alwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;

        playIndefinitely = currentSchedule.NumberOfTracksToPlay <= 0;

        var newCategoryName = currentSchedule.BiblePublicationCategoryName;
        if (!string.Equals(lastCategoryName, newCategoryName, StringComparison.OrdinalIgnoreCase))
        {
            const int newDefault = 1;
            _ = populateListViewAsync(newDefault);
            if (!playIndefinitely)
            {
                dispatchNumberOfTracksToPlay();
            }
        }

        lastCategoryName = newCategoryName;
    }
}
