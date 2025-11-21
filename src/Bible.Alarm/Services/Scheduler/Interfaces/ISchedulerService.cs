namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface ISchedulerService
{
    Task ProcessScheduledTasksAsync();
    Task<bool> HandleAsync();
}