using CommunityToolkit.Maui.Core.Views;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Windows.Media;

namespace CommunityToolkit.Maui.Core.Handlers;

public partial class MediaElementHandler : ViewHandler<MediaElement, MauiMediaElement>, IDisposable
{
    /// <summary>
    /// Maps the <see cref="Core.IMediaElement.ShouldLoopPlayback"/> property between the abstract
    /// <see cref="MediaElement"/> and platform counterpart.
    /// </summary>
    /// <param name="handler">The associated handler.</param>
    /// <param name="mediaElement">The associated <see cref="MediaElement"/> instance.</param>
    public static void ShouldLoopPlayback(MediaElementHandler handler, MediaElement mediaElement)
    {
        handler?.MediaManager?.UpdateShouldLoopPlayback();
    }

    /// <inheritdoc/>
    protected override MauiMediaElement CreatePlatformView()
    {
        MediaManager ??= new(MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} cannot be null"),
                                VirtualView,
                                Dispatcher.GetForCurrentThread() ?? throw new InvalidOperationException($"{nameof(IDispatcher)} cannot be null"));

        // Always use headless mode for audio-only playback
        // Windows MediaPlayer works perfectly in headless mode for audio playback
        var mediaPlatform = MediaManager.CreatePlatformView();

        // Return lightweight view for headless, or wrap MediaPlayerElement if UI exists
        if (mediaPlatform == null)
        {
            // Headless mode - create lightweight MauiMediaElement without MediaPlayerElement
            return new MauiMediaElement();
        }

        // UI mode - we have a MediaPlayerElement
        return new(mediaPlatform);
    }

    /// <summary>
    /// Gets the System Media Transport Controls for Windows.
    /// Returns null if MediaManager is not initialized.
    /// </summary>
    public SystemMediaTransportControls? GetSystemMediaTransportControls()
    {
        return MediaManager?.GetSystemMediaTransportControls();
    }

    /// <inheritdoc/>
    protected override void DisconnectHandler(MauiMediaElement platformView)
    {
        Dispose();
        UnloadPlatformView(platformView);
        base.DisconnectHandler(platformView);
    }

    static void UnloadPlatformView(MauiMediaElement platformView)
    {
        if (platformView.IsLoaded)
        {
            platformView.Unloaded += OnPlatformViewUnloaded;
        }
        else
        {
            platformView.Dispose();
        }

        static void OnPlatformViewUnloaded(object sender, RoutedEventArgs e)
        {
            var mediaElement = (MauiMediaElement)sender;

            mediaElement.Unloaded -= OnPlatformViewUnloaded;
            mediaElement.Dispose();
        }
    }

    partial void PlatformDispose()
    {
        /*
         * Intentionally empty: Windows teardown runs from DisconnectHandler / Unloaded.
         */
    }
}