using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Success action dispatched by Effects after successful DB create operation.
/// Contains the domain model (ScheduleStateItem DTO) with server-generated ID and timestamps.
/// Reducers handle this action (pure, no mapping).
/// </summary>
public class CreateScheduleSuccessAction(ScheduleStateItem schedule)
{
    public ScheduleStateItem Schedule { get; } = schedule;
}

