#if WINDOWS
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;
using Windows.Media;

namespace CommunityToolkit.Maui.Views;

/// <summary>
/// Windows-specific extensions for MediaElement to access System Media Transport Controls.
/// </summary>
public static class MediaElementWindowsExtensions
{
    /// <summary>
    /// Gets the System Media Transport Controls for Windows.
    /// Returns null if MediaElement is not initialized or not on Windows platform.
    /// </summary>
    public static SystemMediaTransportControls? GetSystemMediaTransportControls(this MediaElement mediaElement)
    {
        var handler = mediaElement.Handler as MediaElementHandler;
        return handler?.GetSystemMediaTransportControls();
    }
}
#endif
