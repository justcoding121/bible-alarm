#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

/// <summary>
/// Handles initialization for ScheduleListItemViewModel.
/// </summary>
public sealed class ScheduleListItemInitializer(
    ILogger logger,
    IMapper mapper,
    IState<ApplicationState> applicationState)
{
    /// <summary>
    /// Initializes from pre-mapped AlarmSchedule data.
    /// </summary>
    public (AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem) InitializeFromSchedule(AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem = null)
    {
        if (schedule == null || schedule.Id <= 0)
        {
            logger.Warning("InitializeFromSchedule: Invalid schedule or schedule ID {ScheduleId}", schedule?.Id ?? 0);
            return (null!, null);
        }

        // Get schedule state item if not provided (for subtitle tracking)
        scheduleStateItem ??= applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == schedule.Id);

        return (schedule, scheduleStateItem);
    }

    /// <summary>
    /// Initializes from state using the schedule ID.
    /// </summary>
    public (AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem) SetScheduleId(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            logger.Warning("SetScheduleId: Invalid schedule ID {ScheduleId}", scheduleId);
            return (null!, null);
        }

        // Find schedule from state
        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            logger.Warning("SetScheduleId: Schedule {ScheduleId} not found in state", scheduleId);
            return (null!, null);
        }

        // Map ScheduleStateItem to AlarmSchedule entity
        var schedule = mapper.Map<AlarmSchedule>(scheduleStateItem);

        return (schedule, scheduleStateItem);
    }
}
