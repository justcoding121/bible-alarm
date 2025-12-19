namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface ISchedulerService : IDisposable
{
    Task ProcessScheduledTasksAsync();
    Task<bool> HandleAsync();
    Task RescheduleNextOccurrenceAsync(int scheduleId);
}
