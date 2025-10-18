namespace Bible.Alarm.Contracts.Media;

public interface IAndroidAlarmHandler
{
    Task Handle(long scheduleId, bool isImmediate);
}