namespace Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;

public interface IIosAlarmHandler
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
