using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Failure action dispatched by Effects when DB update operation fails.
/// Used to rollback optimistic updates in reducers.
/// </summary>
public class UpdateScheduleFailureAction(ScheduleStateItem schedule, string error)
{
    public ScheduleStateItem Schedule { get; } = schedule;
    public string Error { get; } = error;
}

