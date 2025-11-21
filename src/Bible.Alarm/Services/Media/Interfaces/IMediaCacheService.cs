namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaCacheService : IDisposable
{
    Task<bool> ExistsAsync(string url);
    string GetCacheFileName(string url);
    string GetCacheFilePath(string url);

    Task<bool> SetupAlarmCacheAsync(int alarmScheduleId);
    Task CleanUpAsync();
}

