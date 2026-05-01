using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Primitives;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;
#if ANDROID
#endif

namespace CommunityToolkit.Maui.Core.Handlers;

public partial class MediaElementHandler : ViewHandler<MediaElement, MauiMediaElement>, IDisposable
{
    /// <summary>
    /// Maps the ShouldLoopPlayback property between the abstract
    /// <see cref="MediaElement"/> and platform counterpart.
    /// </summary>
    /// <param name="handler">The associated handler.</param>
    /// <param name="mediaElement">The associated <see cref="MediaElement"/> instance.</param>
    public static void ShouldLoopPlayback(MediaElementHandler handler, MediaElement mediaElement)
    {
        handler.MediaManager?.UpdateShouldLoopPlayback();
    }

    /// <summary>
    /// Sets the virtual view with fallback for headless mode.
    /// In headless mode, SetVirtualView may fail due to gesture manager setup.
    /// This method handles that gracefully by using reflection as a fallback.
    /// </summary>
    /// <param name="mediaElement">The MediaElement to set as the virtual view.</param>
    [SuppressMessage("SonarAnalyzer.CSharp", "S3011", Justification = "Headless gesture setup fails SetVirtualView; reflection assigns MAUI-internal _virtualView by design.")]
    public void SetVirtualViewWithFallback(MediaElement mediaElement)
    {
        try
        {
            SetVirtualView(mediaElement);
        }
        catch (Exception)
        {
            // In headless mode, SetVirtualView may fail due to gesture manager setup
            // Use reflection to set VirtualView directly (this is a MAUI framework field, not MediaElement-specific)
            var virtualViewField = typeof(ElementHandler).GetField("_virtualView",
                BindingFlags.NonPublic | BindingFlags.Instance);
            virtualViewField?.SetValue(this, mediaElement);
        }
    }

    /// <summary>
    /// Creates the platform view for MediaElement. Can be called directly for headless initialization.
    /// This is a public wrapper around the protected CreatePlatformView() method.
    /// </summary>
    /// <returns>The platform view (null in headless mode).</returns>
    public MauiMediaElement InitializePlatformView()
    {
        return CreatePlatformView();
    }

    /// <summary>
    /// Creates the platform view for MediaElement.
    /// </summary>
    /// <returns>The platform view (null in headless mode).</returns>
    protected override MauiMediaElement CreatePlatformView()
    {
        // Use the handler's MauiContext - guaranteed to be available when PrepareAndPlayAsync is called
        // (bootstrap completes before PrepareAndPlayAsync is called)
        if (MauiContext == null)
        {
            throw new InvalidOperationException("MauiContext is null - ensure bootstrap has completed before calling PrepareAndPlayAsync");
        }

        var dispatcher = GetDispatcher();
        MediaManager ??= new MediaManager(MauiContext, VirtualView, dispatcher);

        // Always use None (headless mode) for audio-only playback
        // This app plays Bible readings and music (audio-only), so no UI view is needed
        // ExoPlayer works perfectly in headless mode for audio playback
        var (_, playerView) = MediaManager.CreatePlatformView(AndroidViewType.None);

        // Return lightweight view for headless, or wrap PlayerView if UI exists
        if (playerView == null)
        {
            // Headless mode - create lightweight MauiMediaElement without PlayerView
            if (Context == null)
            {
                throw new InvalidOperationException("Context is null - cannot create platform view");
            }
            return new MauiMediaElement(Context);
        }

        // UI mode - we have a real context and PlayerView
        if (Context == null)
        {
            throw new InvalidOperationException("Context is null but PlayerView was created");
        }

        return new MauiMediaElement(Context, playerView);
    }

    private static IDispatcher GetDispatcher()
    {
        // Get dispatcher - try current thread first, then Application.Current dispatcher
        // After bootstrap completes, Application.Current.Dispatcher should always be available
        var dispatcher = Dispatcher.GetForCurrentThread() ?? (Application.Current?.Dispatcher);
        if (dispatcher == null)
        {
            throw new InvalidOperationException("Dispatcher cannot be null - ensure bootstrap has completed before calling CreatePlatformView");
        }

        return dispatcher;
    }

    protected override void DisconnectHandler(MauiMediaElement platformView)
    {
        // Handle headless mode where platformView may be null
        if (platformView != null)
        {
            base.DisconnectHandler(platformView);
            platformView.Dispose();
        }

        // Dispose() will handle MediaManager disposal (releases MediaSession and stops MediaControlsService)
        // This works for both UI mode and headless mode
        Dispose();
    }

    partial void PlatformDispose()
    {
        // Intentionally empty: DisconnectHandler disposes the MediaManager and releases platform resources.
    }
}