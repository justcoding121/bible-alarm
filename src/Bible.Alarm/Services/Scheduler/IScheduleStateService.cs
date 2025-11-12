namespace Bible.Alarm.Services.Scheduler;

public interface IScheduleStateService
{
    Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled);
}

