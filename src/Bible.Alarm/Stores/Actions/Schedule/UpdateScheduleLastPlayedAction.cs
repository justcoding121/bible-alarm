namespace Bible.Alarm.Stores.Actions.Schedule;

public class UpdateScheduleLastPlayedAction(int scheduleId, DateTime lastPlayedAtUtc)
{
    public int ScheduleId { get; } = scheduleId;
    public DateTime LastPlayedAtUtc { get; } = lastPlayedAtUtc;
}
