namespace Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;

public interface IWindowsAlarmHandler
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
