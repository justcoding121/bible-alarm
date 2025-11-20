namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaCacheService : IDisposable
{
    Task<bool> Exists(string url);
    string GetCacheFileName(string url);
    string GetCacheFilePath(string url);

    Task<bool> SetupAlarmCache(int alarmScheduleId);
    Task CleanUp();

    Task<string> GetMusicTrackUrl(string languageCode, string lookUpPath);

    Task<string> GetBibleChapterUrl(string languageCode, string pubCode, int bookNumber, int chapter,
        string lookUpPath);
}

