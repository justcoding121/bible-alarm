namespace Bible.Alarm.Common.Interfaces.Media;

public interface IMediaCacheSetupService
{
    Task SetupAlarmCacheAsync(long scheduleId);
}

