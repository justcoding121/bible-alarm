namespace Bible.Alarm.Contracts.Media;

public interface IDownloadService : IDisposable
{
    Task<byte[]> DownloadAsync(string url, string alternativeUrl = null);
    Task<bool> FileExists(string url);
}