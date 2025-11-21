namespace Bible.Alarm.Common.Interfaces.Media;

public interface IAndroidAlarmHandler
{
    Task Handle(int scheduleId, bool isAlarm);
}