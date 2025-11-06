namespace Bible.Alarm.Common.Interfaces.Media;

public interface IAndroidAlarmHandler
{
    Task Handle(long scheduleId, bool isImmediate);
}