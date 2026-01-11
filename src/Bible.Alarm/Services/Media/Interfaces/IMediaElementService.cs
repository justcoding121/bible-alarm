#nullable enable
using CommunityToolkit.Maui;

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// MediaElement operates headlessly and does not require UI attachment.
/// </summary>
public interface IMediaElementService : IDisposable
{
    /// <summary>
    /// Gets the MediaElement instance.
    /// If the MediaElement doesn't exist (was disposed), creates a new one on-demand.
    /// MediaElement operates headlessly without requiring UI attachment.
    /// </summary>
    Task<MediaElement> GetMediaElementAsync();

    /// <summary>
    /// Initializes MediaElement during bootstrap.
    /// OBSOLETE: MediaElement is now created on-demand. This method does nothing.
    /// </summary>
    [Obsolete("MediaElement is now created on-demand. This method does nothing.")]
    Task InitializeMediaElementAsync();

    /// <summary>
    /// Disposes the MediaElement instance and releases resources.
    /// Called when playback stops to free up ExoPlayer and MediaSession resources.
    /// </summary>
    Task DisposeMediaElementAsync();
}

