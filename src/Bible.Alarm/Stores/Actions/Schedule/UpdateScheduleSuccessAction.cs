using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Success action dispatched by Effects after mapping DB entity to DTO.
/// Reducers handle this action (pure, no mapping).
/// </summary>
public class UpdateScheduleSuccessAction(ScheduleStateItem schedule, bool skipCacheRefresh = false)
{
    public ScheduleStateItem Schedule { get; } = schedule;
    
    /// <summary>
    /// If true, indicates this action was dispatched from a cache refresh operation.
    /// The effect handler should skip triggering another cache refresh to prevent cycles.
    /// </summary>
    public bool SkipCacheRefresh { get; } = skipCacheRefresh;
}

