namespace Bible.Alarm.Services.Media.Interfaces;

public interface IDownloadService : IDisposable
{
    Task<byte[]> DownloadAsync(string url, string alternativeUrl = null);
    Task<bool> FileExists(string url);
}

