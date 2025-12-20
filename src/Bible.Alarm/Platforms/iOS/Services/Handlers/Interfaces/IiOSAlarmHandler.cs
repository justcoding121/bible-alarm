namespace Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;

public interface IIOsAlarmHandler : IDisposable
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
