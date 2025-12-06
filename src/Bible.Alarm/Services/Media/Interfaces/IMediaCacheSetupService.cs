namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaCacheSetupService : IDisposable
{
    Task SetupAlarmCacheAsync(int scheduleId);
}

