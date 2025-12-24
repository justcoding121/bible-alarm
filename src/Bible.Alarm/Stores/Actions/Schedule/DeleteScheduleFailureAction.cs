#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Failure action dispatched by Effects when DB delete operation fails.
/// Used to rollback optimistic updates in reducers.
/// </summary>
public class DeleteScheduleFailureAction(int scheduleId, string error, ScheduleStateItem? schedule = null)
{
    public int ScheduleId { get; } = scheduleId;
    public string Error { get; } = error;
    public ScheduleStateItem? Schedule { get; } = schedule;
}

