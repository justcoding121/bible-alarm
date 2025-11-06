namespace Bible.Alarm.Common.Interfaces.Media;

public interface IDownloadService : IDisposable
{
    Task<byte[]> DownloadAsync(string url, string alternativeUrl = null);
    Task<bool> FileExists(string url);
}