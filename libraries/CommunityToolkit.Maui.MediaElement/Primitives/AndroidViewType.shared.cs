using CommunityToolkit.Maui.Views;

namespace CommunityToolkit.Maui.Core;

/// <summary>
/// Enum for <see cref="MediaElement"/> view type on Android
/// </summary>
public enum AndroidViewType
{
    /// <summary>
    /// Create MediaElement on Android using SurfaceView
    /// </summary>
    SurfaceView,

    /// <summary>
    /// Create MediaElement on Android using TextureView
    /// </summary>
    TextureView,

    /// <summary>
    /// Headless mode - no view created (audio-only playback, e.g., Android Auto)
    /// </summary>
    None
}