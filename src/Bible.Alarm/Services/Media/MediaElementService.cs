#nullable enable
using System.Reflection;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Handlers;
using Serilog;
#if ANDROID
#endif

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Thread-safe: ensures MediaElement creation happens on the main thread to prevent deadlocks and crashes.
/// MediaElement is a singleton for the app process lifetime - it is initialized during bootstrap and never destroyed.
/// MediaElement operates headlessly and does not require UI attachment.
/// </summary>
public sealed class MediaElementService : IMediaElementService, IDisposable
{
    private readonly ILogger logger;
    // Local lock for this service
    private readonly Lock lockObject = new();
    // Store MediaElement instance
    // MediaElement operates headlessly and does not require UI attachment
    private MediaElement? mediaElementInstance;
    // Track initialization task to allow waiting for initialization to complete
    private TaskCompletionSource<bool>? initializationTaskSource;
    private volatile bool isInitializing;
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
#endif

    public MediaElementService(ILogger logger)
    {
        this.logger = logger;

        // MediaElement is now a singleton for all platforms - do not register for DestroyMediaElementMessage
        // MediaElement will live for the entire app process lifetime
    }

    public async Task<MediaElement> GetMediaElementAsync()
    {
        // Wait for initialization if it's in progress
        // BUT: If we're on the main thread, we might be the one doing the initialization,
        // so don't wait (to avoid deadlock). Only wait if called from a background thread.
        TaskCompletionSource<bool>? initTask = null;
        lock (lockObject)
        {
            if (isInitializing && initializationTaskSource != null)
            {
                initTask = initializationTaskSource;
            }
        }

        if (initTask != null)
        {
            // Only wait if we're NOT on the main thread
            // If we're on main thread, we're likely the one doing initialization, so proceed
            if (!MainThread.IsMainThread)
            {
                logger.Debug("MediaElement initialization in progress, waiting for completion (from background thread)");
                await initTask.Task.ConfigureAwait(false);
            }
            else
            {
                logger.Debug("MediaElement initialization in progress, but we're on main thread - likely we're the initializer, proceeding directly");
                // Don't wait - we're on main thread and likely part of the initialization process
                // This prevents deadlock when InitializeMediaElementAsync calls GetMediaElementAsync
            }
        }

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
            logger.Error("Failed to create MediaElement on main thread");
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
    /// Initializes MediaElement during bootstrap.
    /// Creates a single MediaElement instance that lives for the app process lifetime.
    /// This ensures ExoPlayer and MediaSession are created early and persist throughout the app lifecycle.
    /// This method is called on a background task during bootstrap and does not block.
    /// </summary>
    public async Task InitializeMediaElementAsync()
    {
        TaskCompletionSource<bool>? taskSource = null;
        bool shouldInitialize = false;

        // Use local lock to synchronize
        lock (lockObject)
        {
            // Check if already initialized
            if (mediaElementInstance != null)
            {
                logger.Debug("MediaElement already initialized during bootstrap");
                return;
            }

            // Check if initialization is already in progress
            if (isInitializing)
            {
                logger.Debug("MediaElement initialization already in progress, waiting for completion");
                taskSource = initializationTaskSource;
            }
            else
            {
                // Start initialization
                isInitializing = true;
                initializationTaskSource = new TaskCompletionSource<bool>();
                shouldInitialize = true;
            }
        }

        // If initialization is already in progress, wait for it
        if (taskSource != null)
        {
            await taskSource.Task.ConfigureAwait(false);
            return;
        }

        // Perform initialization
        try
        {
            logger.Information("[BOOTSTRAP] Initializing MediaElement instance for app lifetime");
            await GetMediaElementAsync();
            logger.Information("[BOOTSTRAP] MediaElement instance initialized successfully");

            // Signal completion
            lock (lockObject)
            {
                initializationTaskSource?.TrySetResult(true);
                isInitializing = false;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[BOOTSTRAP] Error initializing MediaElement");
            lock (lockObject)
            {
                initializationTaskSource?.TrySetException(ex);
                isInitializing = false;
            }
            throw;
        }
    }

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
    private async Task EnsureHandlerCreatedAsync(MediaElement mediaElement)
    {
        logger.Debug($"EnsureHandlerCreated called - handlerCreated={handlerCreated}, globalHandlerCreated={globalHandlerCreated}, mediaElement.Handler != null: {mediaElement.Handler != null}");

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
                globalHandlerCreated = true; // STATIC flag prevents creation across entire app
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

    // MediaElement is now a singleton for all platforms - it is never destroyed during app lifetime
    // No DestroyMediaElementMessage handling needed

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
    }
}

