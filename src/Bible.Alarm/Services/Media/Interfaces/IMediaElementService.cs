#nullable enable
using CommunityToolkit.Maui.Views;

namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Handles creation and retrieval of MediaElement from BootstrapPage.
/// </summary>
public interface IMediaElementService
{
    /// <summary>
    /// Gets the MediaElement instance from BootstrapPage.
    /// If the MediaElement doesn't exist (was disposed), creates a new one.
    /// </summary>
    /// <returns>The MediaElement instance</returns>
    MediaElement GetMediaElement();
}

