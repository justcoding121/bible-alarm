namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaCacheSetupService
{
    Task SetupAlarmCacheAsync(int scheduleId);
}

