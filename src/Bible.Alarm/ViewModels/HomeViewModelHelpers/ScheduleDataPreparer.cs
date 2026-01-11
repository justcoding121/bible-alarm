#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles off-UI-thread data preparation for schedules.
/// Separated from HomeViewModel for better modularity.
/// </summary>
public class ScheduleDataPreparer
{
    private readonly IMapper mapper;

    public ScheduleDataPreparer(IMapper mapper)
    {
        this.mapper = mapper;
    }

    /// <summary>
    /// Prepares schedule data structures off UI thread (IDs, filtering, and mapping).
    /// This moves CPU-intensive operations off the UI thread:
    /// - List snapshot creation
    /// - Filtering invalid schedules
    /// - AutoMapper mapping (ScheduleStateItem -> AlarmSchedule)
    /// Returns prepared data that can be used to initialize ViewModels on UI thread.
    /// </summary>
    public (Dictionary<int, AlarmSchedule> scheduleDataMap, Dictionary<int, ScheduleStateItem> scheduleStateItemMap) PrepareScheduleDataOffUIThread(ObservableHashSet<ScheduleStateItem> scheduleItems)
    {
        var scheduleDataMap = new Dictionary<int, AlarmSchedule>();
        var scheduleStateItemMap = new Dictionary<int, ScheduleStateItem>();

        // Create a snapshot of the schedule items collection to avoid "Collection was modified" exception
        var scheduleItemsSnapshot = scheduleItems.ToList();

        // Process each schedule item from the snapshot
        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            var scheduleId = scheduleItem.Id;

            // Skip unsaved schedules (ID <= 0) - they should not appear in the home list
            if (scheduleId <= 0)
            {
                continue;
            }

            // Store ScheduleStateItem for subtitle tracking (needed by InitializeFromSchedule)
            scheduleStateItemMap[scheduleId] = scheduleItem;

            // Map ScheduleStateItem to AlarmSchedule entity (CPU-intensive, can be off UI thread)
            var schedule = mapper.Map<AlarmSchedule>(scheduleItem);
            scheduleDataMap[scheduleId] = schedule;
        }

        return (scheduleDataMap, scheduleStateItemMap);
    }
}

