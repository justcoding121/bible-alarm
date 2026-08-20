using AVKit;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;

namespace CommunityToolkit.Maui.Core.Handlers;

public partial class MediaElementHandler : ViewHandler<MediaElement, MauiMediaElement>, IDisposable
{
    AVPlayerViewController? playerViewController;

    /// <inheritdoc/>
    /// <exception cref="NullReferenceException">Thrown if <see cref="MauiContext"/> is <see langword="null"/>.</exception>
    protected override MauiMediaElement CreatePlatformView()
    {
        if (MauiContext is null)
        {
            throw new InvalidOperationException($"{nameof(MauiContext)} cannot be null");
        }

        MediaManager ??= new(MauiContext,
            VirtualView,
            Dispatcher.GetForCurrentThread() ?? throw new InvalidOperationException($"{nameof(IDispatcher)} cannot be null"));

        // Always use headless mode for audio-only playback
        // AVPlayer works perfectly in headless mode for audio playback
        var (_, avPlayerViewController) = MediaManager.CreatePlatformView();

        if (avPlayerViewController == null)
        {
            return new MauiMediaElement();
        }

        return new(avPlayerViewController, VirtualView);
    }

    /// <inheritdoc/>
    protected override void DisconnectHandler(MauiMediaElement platformView)
    {
        platformView.Dispose();
        Dispose();

        base.DisconnectHandler(platformView);
    }

    partial void PlatformDispose()
    {
        playerViewController?.Dispose();
        playerViewController = null;
    }
}