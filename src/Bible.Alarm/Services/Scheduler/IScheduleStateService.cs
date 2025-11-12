namespace Bible.Alarm.Services.Scheduler;

public interface IScheduleStateService
{
    Task<bool> UpdateScheduleEnabledStateAsync(long scheduleId, bool isEnabled);
}

