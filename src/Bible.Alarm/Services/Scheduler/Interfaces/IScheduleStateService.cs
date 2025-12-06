namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface IScheduleStateService : IDisposable
{
    Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled);
}

