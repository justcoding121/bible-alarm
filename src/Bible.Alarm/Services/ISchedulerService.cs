namespace Bible.Alarm.Services;

public interface ISchedulerService
{
    Task ProcessScheduledTasks();
}