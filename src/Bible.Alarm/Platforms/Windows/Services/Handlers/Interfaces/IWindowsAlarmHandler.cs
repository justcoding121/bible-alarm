namespace Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;

public interface IWindowsAlarmHandler : IDisposable
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
