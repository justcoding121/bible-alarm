#nullable enable
using CommunityToolkit.Maui.Views;

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Handles creation and retrieval of MediaElement from BootstrapPage.
/// MediaElement can exist without being attached to BootstrapPage container (e.g., when app is backgrounded).
/// </summary>
public interface IMediaElementService : IDisposable
{
    /// <summary>
    /// Gets the MediaElement instance.
    /// If the MediaElement doesn't exist (was disposed), creates a new one.
    /// MediaElement will be attached to BootstrapPage container if available, otherwise works without UI container.
    /// </summary>
    /// <returns>The MediaElement instance</returns>
    Task<MediaElement> GetMediaElementAsync();

    /// <summary>
    /// Reattaches MediaElement to BootstrapPage container when BootstrapPage becomes available.
    /// Called from BootstrapPage.OnAppearing when page is recreated.
    /// </summary>
    void ReattachMediaElementIfNeeded();
}

