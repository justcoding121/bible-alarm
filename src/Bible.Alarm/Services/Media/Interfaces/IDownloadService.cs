#nullable enable
namespace Bible.Alarm.Services.Media.Interfaces;

public interface IDownloadService : IDisposable
{
    Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file with progress reporting.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="progressCallback">Called with (bytesDownloaded, totalBytes). totalBytes may be null if unknown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The downloaded bytes.</returns>
    Task<byte[]> DownloadWithProgressAsync(string url, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default);
    
    Task<bool> FileExists(string url);
}

