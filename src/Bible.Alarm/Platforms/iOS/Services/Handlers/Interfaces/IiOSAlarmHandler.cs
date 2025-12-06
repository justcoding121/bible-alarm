namespace Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;

public interface IiOSAlarmHandler : IDisposable
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
