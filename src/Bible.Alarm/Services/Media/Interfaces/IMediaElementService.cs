#nullable enable
using CommunityToolkit.Maui.Views;

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// MediaElement operates headlessly and does not require UI attachment.
/// </summary>
public interface IMediaElementService : IDisposable
{
    /// <summary>
    /// Gets the MediaElement instance.
    /// If the MediaElement doesn't exist (was disposed), creates a new one.
    /// MediaElement operates headlessly without requiring UI attachment.
    /// </summary>
    Task<MediaElement> GetMediaElementAsync();

    /// <summary>
    /// Initializes MediaElement during bootstrap.
    /// Creates a single MediaElement instance that lives for the app process lifetime.
    /// This is called on a background task during bootstrap and does not block.
    /// </summary>
    Task InitializeMediaElementAsync();
}

