namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Success action dispatched by Effects after mapping DB entity to DTO.
/// Reducers handle this action (pure, no mapping).
/// </summary>
public class RemoveScheduleSuccessAction(int scheduleId)
{
    public int ScheduleId { get; } = scheduleId;
}

