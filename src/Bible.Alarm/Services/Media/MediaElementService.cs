#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.Primitives;
using Microsoft.Maui.Handlers;
using Serilog;
#if ANDROID
#endif
#if IOS
using UIKit;
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Thread-safe: ensures MediaElement creation happens on the main thread to prevent deadlocks and crashes.
/// MediaElement is created on-demand when playback starts and disposed when playback stops.
/// MediaElement operates headlessly and does not require UI attachment.
/// </summary>
public sealed partial class MediaElementService : IMediaElementService
{
    private readonly ILogger logger;
    // Local lock for this service
    private readonly Lock lockObject = new();
    // Store MediaElement instance
    // MediaElement operates headlessly and does not require UI attachment
    private MediaElement? mediaElementInstance;
#if ANDROID
    // Track if handler has been created to prevent duplicate ExoPlayer creation
    // Used in debug logging and conditional checks
    private bool handlerCreated;
#endif

#if ANDROID
    // STATIC flag to prevent duplicate ExoPlayer creation across entire application
    // This is critical because MediaElementService might be instantiated multiple times
    // Used in conditional checks to prevent duplicate ExoPlayer creation
    private static bool globalHandlerCreated;

    private static void ResetGlobalHandlerCreatedFlag() => globalHandlerCreated = false;

    private static void MarkGlobalHandlerCreatedFlag() => globalHandlerCreated = true;
#endif

    public MediaElementService(ILogger logger)
    {
        this.logger = logger;

        // MediaElement is now created on-demand - no early initialization needed
    }

    public async Task<MediaElement> GetMediaElementAsync()
    {
        MediaElement? existingInstance;

        // Use local lock to synchronize access to MediaElement
        lock (lockObject)
        {
            // First, check if we already have a MediaElement instance (even if not attached to UI)
            existingInstance = mediaElementInstance;
        }

        if (existingInstance != null)
        {
            logger.Debug("MediaElement instance found in service");

            // Ensure handler exists for existing MediaElement (all platforms)
            await EnsureHandlerCreatedAsync(existingInstance).ConfigureAwait(false);

            return existingInstance;
        }

        // MediaElement doesn't exist, create a new one
        // MUST be done on main thread - MediaElement creation must be on UI thread
        logger.Information("MediaElement not found, creating new instance on main thread");

        MediaElement? newMediaElement = null;

        // Always create MediaElement on main thread
        if (MainThread.IsMainThread)
        {
            newMediaElement = CreateMediaElementOnMainThread();
        }
        else
        {
            // Asynchronously invoke on main thread to ensure MediaElement is created before returning
            // This prevents race conditions where MediaElement is accessed before it's fully created
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                newMediaElement = CreateMediaElementOnMainThread();
            });
        }

        if (newMediaElement == null)
        {
            throw new InvalidOperationException("Failed to create MediaElement - could not create on main thread");
        }

        // Store the instance immediately
        lock (lockObject)
        {
            mediaElementInstance = newMediaElement;
        }

        logger.Information("New MediaElement created and stored in service");

        // Create handler immediately for headless mode (all platforms)
        // Handler must exist before Source is set (MapSource requires handler)
        await EnsureHandlerCreatedAsync(newMediaElement).ConfigureAwait(false);

        return newMediaElement;
    }

    /// <summary>
    /// Disposes the MediaElement instance and releases resources.
    /// Called when playback stops to free up ExoPlayer and MediaSession resources.
    /// </summary>
    public async Task DisposeMediaElementAsync()
    {
        MediaElement? toDispose = null;

        lock (lockObject)
        {
            toDispose = mediaElementInstance;
            mediaElementInstance = null;

#if ANDROID
            // Reset flags to allow new MediaElement creation
            handlerCreated = false;
            ResetGlobalHandlerCreatedFlag();
#endif
        }

        if (toDispose == null)
        {
            logger.Debug("No MediaElement instance to dispose");
            return;
        }

        try
        {
            logger.Information("Disposing MediaElement instance and releasing resources");

            // Dispose on main thread
            if (MainThread.IsMainThread)
            {
                DisposeMediaElementOnMainThread(toDispose);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => DisposeMediaElementOnMainThread(toDispose));
            }

            logger.Information("MediaElement disposed successfully");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error disposing MediaElement");
        }
    }

    private void DisposeMediaElementOnMainThread(MediaElement mediaElement)
    {
        if (!MainThread.IsMainThread)
        {
            logger.Warning("DisposeMediaElementOnMainThread called from non-main thread");
        }

        try
        {
            StopMediaElementPlaybackIfNeeded(mediaElement);
            mediaElement.Source = null;

#if IOS
            var nativePlatformView = TryReadIosPlatformViewBeforeHandlerDispose(mediaElement);
#endif

            var handler = mediaElement.Handler;

#if IOS
            CleanupIosNativeViewsAfterHandlerDisposed(nativePlatformView, handler);
#endif

            DisposeMediaElementHandlerAndClearBinding(mediaElement, handler);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error during MediaElement disposal - continuing");
        }
    }

    private static void StopMediaElementPlaybackIfNeeded(MediaElement mediaElement)
    {
        if (mediaElement.CurrentState is MediaElementState.Playing or
            MediaElementState.Paused or
            MediaElementState.Buffering)
        {
            mediaElement.Stop();
        }
    }

#if IOS
    private UIView? TryReadIosPlatformViewBeforeHandlerDispose(MediaElement mediaElement)
    {
        try
        {
            return mediaElement.Handler?.PlatformView as UIView;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "MediaElement disposal: could not read PlatformView before handler dispose");
            return null;
        }
    }
#endif

    [SuppressMessage("SonarAnalyzer.CSharp", "S3011",
        Justification = "Clears MAUI Element._handler via reflection after handler dispose; controlled teardown without public MAUI API.")]
    private static void DisposeMediaElementHandlerAndClearBinding(MediaElement mediaElement, IElementHandler? handler)
    {
        if (handler is IDisposable disposableHandler)
        {
            disposableHandler.Dispose();
        }

        var handlerField = typeof(Element).GetField("_handler",
            BindingFlags.NonPublic | BindingFlags.Instance);
        handlerField?.SetValue(mediaElement, null);
    }

#if IOS
    private void CleanupIosNativeViewsAfterHandlerDisposed(UIView? nativePlatformView, IElementHandler? handler)
    {
        UIKit.UIViewController? viewController = null;
        try
        {
            if (handler is IPlatformViewHandler pvh)
            {
                viewController = pvh.ViewController;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "MediaElement disposal: could not read ViewController before handler dispose");
        }

        if (nativePlatformView != null)
        {
            try
            {
                IosNativeViewCleanupHelper.SuppressFinalizersForViewHierarchy(nativePlatformView);
            }
            catch (Exception)
            {
                // Best-effort ObjC/native cleanup during MediaElement teardown; continue if runtime throws.
            }
        }

        if (viewController != null)
        {
            try
            {
                if (viewController.View != null)
                {
                    IosNativeViewCleanupHelper.SuppressFinalizersForViewHierarchy(viewController.View);
                }
            }
            catch (ObjectDisposedException ex)
            {
                logger.Debug(ex, "MediaElement disposal: view controller already disposed during cleanup");
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "MediaElement disposal: view controller cleanup failed");
            }
        }
    }
#endif

    /// <summary>
    /// Creates a new MediaElement instance on the main thread.
    /// Android: Handler must be created manually in headless mode (no parent view).
    /// Handler is required before Source can be set (MapSource requires handler to exist).
    /// </summary>
    private MediaElement CreateMediaElementOnMainThread()
    {
        // This method must be called on the main thread
        if (!MainThread.IsMainThread)
        {
            logger.Warning("CreateMediaElementOnMainThread called from non-main thread - this should not happen");
        }

        var newMediaElement = new MediaElement
        {
            IsVisible = false,
            ShouldAutoPlay = false,
            ShouldLoopPlayback = false,
            ShouldShowPlaybackControls = false
        };

        return newMediaElement;
    }

#if ANDROID
    /// <summary>
    /// Ensures MediaElement handler is created for headless Android operation.
    /// Must be called on main thread after bootstrap completes.
    /// </summary>
    [SuppressMessage("SonarAnalyzer.CSharp", "S3011", Justification = "Headless mode: MAUI does not expose handler wiring; reflection matches SetVirtualViewWithFallback behavior.")]
    private async Task EnsureHandlerCreatedAsync(MediaElement mediaElement)
    {
        logger.Debug(
            "EnsureHandlerCreated called - handlerCreated={HandlerCreated}, globalHandlerCreated={GlobalHandlerCreated}, MediaElement.Handler is not null: {HasHandler}",
            handlerCreated,
            globalHandlerCreated,
            mediaElement.Handler != null);

        // CRITICAL: Check STATIC flag first to prevent duplicate ExoPlayer creation across entire application
        lock (lockObject)
        {
            if (globalHandlerCreated)
            {
                logger.Debug("GLOBAL: Handler already created (static flag check), skipping duplicate creation");
                return;
            }
        }

        if (mediaElement.Handler != null)
        {
            lock (lockObject)
            {
                handlerCreated = true;
            }
            logger.Debug("MediaElement handler already exists");
            return;
        }

        if (!MainThread.IsMainThread)
        {
            logger.Debug("EnsureHandlerCreated called from non-main thread - marshalling to main thread");
            await MainThread.InvokeOnMainThreadAsync(() => EnsureHandlerCreatedAsync(mediaElement)).ConfigureAwait(false);
            return;
        }

        try
        {
            // Get MauiContext from Application.Current (available after bootstrap)
            var appHandler = Application.Current?.Handler;
            if (appHandler?.MauiContext == null)
            {
                logger.Warning("Cannot create handler - Application.Current.Handler.MauiContext is null. Bootstrap may not have completed.");
                return;
            }

            var mauiContext = appHandler.MauiContext;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                logger.Warning("Cannot create handler - Application.Current.Dispatcher is null. Bootstrap may not have completed.");
                return;
            }

            // Create handler manually
            var handler = new MediaElementHandler();
            handler.SetMauiContext(mauiContext);

            // Set VirtualView with fallback for headless mode
            // The fallback logic is now encapsulated in SetVirtualViewWithFallback
            handler.SetVirtualViewWithFallback(mediaElement);

            // Trigger InitializePlatformView to initialize MediaManager and ExoPlayer
            // This will return null in headless mode (expected)
            // InitializePlatformView is a public wrapper around CreatePlatformView
            handler.InitializePlatformView();

            // Attach handler to MediaElement
            try
            {
                mediaElement.Handler = handler;
            }
            catch (Exception ex)
            {
                // Gesture manager setup may fail - use reflection fallback
                logger.Debug(ex, "Failed to set handler via property (expected in headless mode) - using reflection fallback");
                var handlerField = typeof(Element).GetField("_handler",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                handlerField?.SetValue(mediaElement, handler);
            }

            // Mark handler as created to prevent duplicate ExoPlayer creation
            lock (lockObject)
            {
                handlerCreated = true;
                MarkGlobalHandlerCreatedFlag();
            }

            logger.Information("MediaElement handler created successfully for headless Android operation");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create MediaElement handler - handler will be created when Source is set");
        }
    }
#endif

    /// <summary>
    /// Ensures MediaElement handler is created for headless operation (Windows/iOS).
    /// Must be called on main thread after bootstrap completes.
    /// </summary>
#if !ANDROID
    [SuppressMessage("SonarAnalyzer.CSharp", "S3011", Justification = "Headless mode: MAUI does not expose handler wiring; reflection is the supported fork pattern.")]
    private async Task EnsureHandlerCreatedAsync(MediaElement mediaElement)
    {
        if (mediaElement.Handler != null)
        {
            logger.Debug("MediaElement handler already exists");
            return;
        }

        if (!MainThread.IsMainThread)
        {
            logger.Debug("EnsureHandlerCreated called from non-main thread - marshalling to main thread");
            await MainThread.InvokeOnMainThreadAsync(() => EnsureHandlerCreatedAsync(mediaElement)).ConfigureAwait(false);
            return;
        }

        try
        {
            // Get MauiContext from Application.Current (available after bootstrap)
            var appHandler = Application.Current?.Handler;
            if (appHandler?.MauiContext == null)
            {
                logger.Warning("Cannot create handler - Application.Current.Handler.MauiContext is null. Bootstrap may not have completed.");
                return;
            }

            var mauiContext = appHandler.MauiContext;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                logger.Warning("Cannot create handler - Application.Current.Dispatcher is null. Bootstrap may not have completed.");
                return;
            }

            // Create handler manually
            var handler = new MediaElementHandler();
            handler.SetMauiContext(mauiContext);

            // Set VirtualView with fallback for headless mode
            try
            {
                handler.SetVirtualView(mediaElement);
            }
            catch (Exception ex)
            {
                // In headless mode, SetVirtualView may fail - use reflection fallback
                logger.Debug(ex, "Failed to set VirtualView (expected in headless mode) - using reflection fallback");
                var virtualViewField = typeof(ElementHandler).GetField("_virtualView",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                virtualViewField?.SetValue(handler, mediaElement);
            }

            // Trigger CreatePlatformView to initialize MediaManager
            // Accessing PlatformView property will automatically call CreatePlatformView
            _ = handler.PlatformView;

            // Attach handler to MediaElement
            try
            {
                mediaElement.Handler = handler;
            }
            catch (Exception ex)
            {
                // Handler setup may fail - use reflection fallback
                logger.Debug(ex, "Failed to set handler via property (expected in headless mode) - using reflection fallback");
                var handlerField = typeof(Element).GetField("_handler",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                handlerField?.SetValue(mediaElement, handler);
            }

            logger.Information("MediaElement handler created successfully for headless operation");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create MediaElement handler - handler will be created when Source is set");
        }
    }
#endif

    // MediaElement is now created on-demand and disposed when playback stops
    // No DestroyMediaElementMessage handling needed

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        MediaElement? toDispose;

        lock (lockObject)
        {
            toDispose = mediaElementInstance;
            mediaElementInstance = null;
        }

        if (toDispose == null)
        {
            return;
        }

        // Best-effort cleanup at app shutdown. If already on the main thread, dispose synchronously.
        // Otherwise, schedule it — disposal at shutdown is fire-and-forget.
        if (MainThread.IsMainThread)
        {
            DisposeMediaElementOnMainThread(toDispose);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => DisposeMediaElementOnMainThread(toDispose));
        }
    }
}

