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
    
    /// <summary>
    /// Gets the Content-Length of a file via HEAD request without downloading it.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Content-Length if available, null otherwise.</returns>
    Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken = default);
    
    Task<bool> FileExists(string url);
}

