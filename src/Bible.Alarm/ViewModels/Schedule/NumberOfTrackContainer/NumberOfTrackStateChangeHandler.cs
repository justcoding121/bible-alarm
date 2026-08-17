#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

/// <summary>
/// Mirrors notification / always-play-from-start / play-indefinitely fields when ApplicationState changes for the same schedule.
/// </summary>
public static class NumberOfTrackStateChangeHandler
{
    /// <summary>Mutable VM fields updated by <see cref="ApplyPropertyChanges"/>.</summary>
    public sealed class SyncTargets
    {
        public bool NotificationEnabled { get; set; }
        public bool AlwaysPlayFromStart { get; set; }
        public bool PlayIndefinitely { get; set; }
        public string? LastCategoryName { get; set; }
    }

    public sealed record ApplyContext(
        ScheduleStateItem CurrentSchedule,
        bool IsWaitingForPermissionResponse,
        Func<bool> GetIsGranted,
        Action DispatchNotificationEnabledOff,
        Func<int, Task> PopulateListViewAsync,
        Action DispatchNumberOfTracksToPlay,
        ILogger Logger);

    public static void ApplyPropertyChanges(ApplyContext context, SyncTargets targets)
    {
        var currentSchedule = context.CurrentSchedule;
        if (!context.IsWaitingForPermissionResponse && targets.NotificationEnabled != currentSchedule.NotificationEnabled)
        {
            var newValue = currentSchedule.NotificationEnabled;
#if ANDROID || IOS
            newValue = NotificationPermissionSyncHelper.SyncValueWithPermission(
                newValue,
                context.GetIsGranted,
                context.Logger,
                "OnStateChanged: NotificationEnabled is true in state but permission is not granted - syncing to OFF",
                "OnStateChanged: Exception checking notification permission",
                context.DispatchNotificationEnabledOff);
#endif
            targets.NotificationEnabled = newValue;
        }

        targets.AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;

        targets.PlayIndefinitely = currentSchedule.NumberOfTracksToPlay <= 0;

        var newCategoryName = currentSchedule.BiblePublicationCategoryName;
        if (!string.Equals(targets.LastCategoryName, newCategoryName, StringComparison.OrdinalIgnoreCase))
        {
            const int newDefault = 1;
            _ = context.PopulateListViewAsync(newDefault);
            if (!targets.PlayIndefinitely)
            {
                context.DispatchNumberOfTracksToPlay();
            }
        }

        targets.LastCategoryName = newCategoryName;
    }
}
