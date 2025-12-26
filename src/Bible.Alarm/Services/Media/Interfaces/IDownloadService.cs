namespace Bible.Alarm.Services.Media.Interfaces;

public interface IDownloadService : IDisposable
{
    Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null, CancellationToken cancellationToken = default);
    Task<bool> FileExists(string url);
}

