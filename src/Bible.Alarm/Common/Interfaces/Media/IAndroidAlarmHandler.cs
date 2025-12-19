namespace Bible.Alarm.Common.Interfaces.Media;

public interface IAndroidAlarmHandler : IDisposable
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
