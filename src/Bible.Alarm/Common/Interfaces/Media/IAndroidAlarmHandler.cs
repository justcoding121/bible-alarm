namespace Bible.Alarm.Common.Interfaces.Media;

public interface IAndroidAlarmHandler
{
    Task HandleAsync(int scheduleId, bool isAlarm);
}
