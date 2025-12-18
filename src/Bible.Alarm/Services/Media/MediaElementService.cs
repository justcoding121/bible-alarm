#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Serilog;
#if ANDROID
using Android.App;
using Android.Content;
#endif

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Handles creation and retrieval of MediaElement from BootstrapPage.
/// Thread-safe: ensures MediaElement creation happens on the main thread to prevent deadlocks and crashes.
/// Also handles MediaElement disposal when DestroyMediaElementMessage is received.
/// MediaElement can exist without being attached to BootstrapPage container (e.g., when app is backgrounded).
/// When BootstrapPage becomes available, MediaElement will be reattached automatically.
/// </summary>
public class MediaElementService : IMediaElementService, IRecipient<DestroyMediaElementMessage>, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;
    // Local lock for this service
    private readonly object _lockObject = new object();
    // Store MediaElement instance independently of BootstrapPage container
    // This allows MediaElement to exist when app is backgrounded (no UI)
    private MediaElement? _mediaElementInstance;
    // Track if handler has been created to prevent duplicate ExoPlayer creation
    private bool _handlerCreated = false;

    // STATIC flag to prevent duplicate ExoPlayer creation across entire application
    // This is critical because MediaElementService might be instantiated multiple times
    private static bool _globalHandlerCreated = false;

    public MediaElementService(INavigationService navigationService, ILogger logger)
    {
        _navigationService = navigationService;
        _logger = logger;
        
        // Register for DestroyMediaElementMessage to handle MediaElement disposal
        WeakReferenceMessenger.Default.Register<DestroyMediaElementMessage>(this);
    }

    public async Task<MediaElement> GetMediaElementAsync()
    {
        MediaElement? existingInstance;
        
        // Use local lock to synchronize access to MediaElement
        lock (_lockObject)
        {
            // First, check if we already have a MediaElement instance (even if not attached to UI)
            existingInstance = _mediaElementInstance;
        }
        
        if (existingInstance != null)
        {
            _logger.Debug("MediaElement instance found in service");
            
#if !ANDROID
            // Try to attach to BootstrapPage if it's available (iOS/Windows only)
            // Android: Skip UI attachment - MediaElement runs headlessly
            TryAttachToBootstrapPage(existingInstance);
#else
            // Android: Ensure handler exists for existing MediaElement
            await EnsureHandlerCreatedAsync(existingInstance).ConfigureAwait(false);
#endif
            
            return existingInstance;
        }

        // MediaElement doesn't exist, create a new one
        // MUST be done on main thread - MediaElement creation must be on UI thread
        _logger.Information("MediaElement not found, creating new instance on main thread");
        
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
            _logger.Error("Failed to create MediaElement on main thread");
            throw new InvalidOperationException("Failed to create MediaElement - could not create on main thread");
        }

        // Store the instance immediately - MediaElement can work without UI attachment
        lock (_lockObject)
        {
            _mediaElementInstance = newMediaElement;
        }
        
        _logger.Information("New MediaElement created and stored in service (will attempt UI attachment separately)");
        
#if ANDROID
        // Android: Create handler immediately for headless mode
        // Handler must exist before Source is set (MapSource requires handler)
        await EnsureHandlerCreatedAsync(newMediaElement).ConfigureAwait(false);
#else
        // Try to attach to BootstrapPage if it's available (iOS/Windows only)
        // MediaElement will work for playback even if attachment fails (e.g., UI is disposed)
        TryAttachToBootstrapPage(newMediaElement);
#endif
        
        return newMediaElement;
    }

    /// <summary>
    /// Creates a new MediaElement instance on the main thread.
    /// Does not attach to BootstrapPage - that's handled separately by TryAttachToBootstrapPage.
    /// Android: Handler must be created manually in headless mode (no parent view).
    /// Handler is required before Source can be set (MapSource requires handler to exist).
    /// </summary>
    private MediaElement CreateMediaElementOnMainThread()
    {
        // This method must be called on the main thread
        if (!MainThread.IsMainThread)
        {
            _logger.Warning("CreateMediaElementOnMainThread called from non-main thread - this should not happen");
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
        _logger.Debug($"EnsureHandlerCreated called - _handlerCreated={_handlerCreated}, _globalHandlerCreated={_globalHandlerCreated}, mediaElement.Handler != null: {mediaElement.Handler != null}");

        // CRITICAL: Check STATIC flag first to prevent duplicate ExoPlayer creation across entire application
        lock (_lockObject)
        {
            if (_globalHandlerCreated)
            {
                _logger.Debug("GLOBAL: Handler already created (static flag check), skipping duplicate creation");
                return;
            }
        }
        
        if (mediaElement.Handler != null)
        {
            lock (_lockObject)
            {
                _handlerCreated = true;
            }
            _logger.Debug("MediaElement handler already exists");
            return;
        }

        if (!MainThread.IsMainThread)
        {
            _logger.Debug("EnsureHandlerCreated called from non-main thread - marshalling to main thread");
            await MainThread.InvokeOnMainThreadAsync(() => EnsureHandlerCreatedAsync(mediaElement)).ConfigureAwait(false);
            return;
        }

        try
        {
            // Get MauiContext from Application.Current (available after bootstrap)
            var appHandler = Microsoft.Maui.Controls.Application.Current?.Handler;
            if (appHandler?.MauiContext == null)
            {
                _logger.Warning("Cannot create handler - Application.Current.Handler.MauiContext is null. Bootstrap may not have completed.");
                return;
            }

            var mauiContext = appHandler.MauiContext;
            var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _logger.Warning("Cannot create handler - Application.Current.Dispatcher is null. Bootstrap may not have completed.");
                return;
            }

            // Create handler manually
            var handler = new MediaElementHandler();
            handler.SetMauiContext(mauiContext);

            // Set VirtualView - this may fail in headless mode due to gesture manager, but we'll handle it
            try
            {
                handler.SetVirtualView(mediaElement);
            }
            catch (Exception ex)
            {
                //In headless mode, SetVirtualView may fail due to gesture manager setup
                // Use reflection to set VirtualView directly
                _logger.Debug(ex, "SetVirtualView failed (expected in headless mode) - using reflection fallback");
                var virtualViewField = typeof(Microsoft.Maui.Handlers.ElementHandler).GetField("_virtualView",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                virtualViewField?.SetValue(handler, mediaElement);
            }

            // Trigger CreatePlatformView to initialize MediaManager and ExoPlayer
            // This will return null in headless mode (expected)
            // Use reflection since CreatePlatformView is protected
            var createPlatformViewMethod = typeof(MediaElementHandler).GetMethod("CreatePlatformView", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            createPlatformViewMethod?.Invoke(handler, null);
            
            // Attach handler to MediaElement
            try
            {
                mediaElement.Handler = handler;
            }
            catch (Exception ex)
            {
                // Gesture manager setup may fail - use reflection fallback
                _logger.Debug(ex, "Failed to set handler via property (expected in headless mode) - using reflection fallback");
                var handlerField = typeof(Microsoft.Maui.Controls.Element).GetField("_handler", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                handlerField?.SetValue(mediaElement, handler);
            }

            // Mark handler as created to prevent duplicate ExoPlayer creation
            lock (_lockObject)
            {
                _handlerCreated = true;
                _globalHandlerCreated = true; // STATIC flag prevents creation across entire app
            }
            
            _logger.Information("MediaElement handler created successfully for headless Android operation");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to create MediaElement handler - handler will be created when Source is set");
        }
    }
#endif

    /// <summary>
    /// Tries to attach MediaElement to BootstrapPage container if BootstrapPage is available.
    /// This allows MediaElement to work when app is backgrounded (no UI) and reattach when UI is recreated.
    /// Non-blocking: Passes shouldRetry=false to prevent indefinite blocking when navigation is not available.
    /// Safe from circular calls: Checks if already attached before attaching to prevent infinite loops.
    /// CRITICAL: UI operations must be on main thread - this method handles thread switching automatically.
    /// MediaElement will function for playback even if attachment fails (e.g., UI is disposed).
    /// </summary>
    private void TryAttachToBootstrapPage(MediaElement mediaElement)
    {
        try
        {
            // Pass shouldRetry=false to avoid blocking when navigation is not available (e.g., app is backgrounded)
            var bootstrapPage = _navigationService.GetBootstrapPage(shouldRetry: false);
            if (bootstrapPage == null)
            {
                _logger.Debug("BootstrapPage not available - MediaElement will work without UI container (background mode)");
                return;
            }

            var mediaElementContainer = bootstrapPage.MediaElementContainerInstance;
            if (mediaElementContainer == null)
            {
                _logger.Warning("MediaElementContainerInstance is null on BootstrapPage");
                return;
            }

            // CRITICAL: Setting Content property requires UI dispatcher
            // Check if we're on main thread, if not, invoke on main thread
            if (MainThread.IsMainThread)
            {
                AttachMediaElementToContainer(mediaElement, mediaElementContainer);
            }
            else
            {
                // Fire and forget - don't block background threads (e.g., Android Auto callbacks)
                // MediaElement can work without UI attachment, so this is non-critical
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        AttachMediaElementToContainer(mediaElement, mediaElementContainer);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to attach MediaElement to container on main thread");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - MediaElement can work without UI container
            _logger.Warning(ex, "Failed to attach MediaElement to BootstrapPage - MediaElement will work without UI container");
        }
    }

    /// <summary>
    /// Attaches MediaElement to container. Must be called on main thread.
    /// </summary>
    private void AttachMediaElementToContainer(MediaElement mediaElement, Microsoft.Maui.Controls.ContentView mediaElementContainer)
    {
        try
        {
            // Check if the container's handler is still valid before trying to attach
            // If the handler is null or disposed, the MauiContext is no longer available
            if (mediaElementContainer.Handler == null)
            {
                _logger.Debug("MediaElementContainer handler is null - container may be disposed, skipping attachment");
                return;
            }

            // Check if the handler's MauiContext is still valid by trying to access a service
            // This will throw ObjectDisposedException if the context is disposed
            try
            {
                var mauiContext = mediaElementContainer.Handler.MauiContext;
                if (mauiContext == null)
                {
                    _logger.Debug("MediaElementContainer MauiContext is null - container may be disposed, skipping attachment");
                    return;
                }

                // Try to access the service provider to verify it's not disposed
                // This will throw ObjectDisposedException if disposed
                _ = mauiContext.Services;
            }
            catch (ObjectDisposedException)
            {
                _logger.Debug("MediaElementContainer MauiContext is disposed - skipping attachment");
                return;
            }

            // Only attach if not already attached - prevents re-attachment and potential circular calls
            if (mediaElementContainer.Content != mediaElement)
            {
                _logger.Debug("Attaching MediaElement to BootstrapPage container");
                
                // Wrap in try-catch because setting Content might fail if MauiContext is disposed
                // This can happen if the page was disposed between getting the reference and setting Content
                try
                {
                    mediaElementContainer.Content = mediaElement;
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.Warning(ex, "MediaElementContainer MauiContext was disposed while setting Content - MediaElement will work without UI container");
                    return;
                }
            }
            else
            {
                _logger.Debug("MediaElement already attached to BootstrapPage container - skipping");
            }
        }
        catch (ObjectDisposedException ex)
        {
            _logger.Warning(ex, "MediaElementContainer or its context is disposed - skipping attachment");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error attaching MediaElement to container - MediaElement will work without UI container");
        }
    }

    /// <summary>
    /// Public method to reattach MediaElement when BootstrapPage becomes available.
    /// Called from BootstrapPage.OnAppearing when page is recreated.
    /// </summary>
    public void ReattachMediaElementIfNeeded()
    {
#if !ANDROID
        // Android: Skip UI attachment - MediaElement runs headlessly
        lock (_lockObject)
        {
            if (_mediaElementInstance != null)
            {
                _logger.Information("Reattaching MediaElement to BootstrapPage container");
                TryAttachToBootstrapPage(_mediaElementInstance);
            }
        }
#endif
    }

    /// <summary>
    /// Handles DestroyMediaElementMessage by disposing the MediaElement and setting container content to null.
    /// This is called after MediaSession release to ensure a clean ExoPlayer instance.
    /// MediaElement will be recreated automatically by GetMediaElementAsync() when needed for the next playlist.
    /// </summary>
    public void Receive(DestroyMediaElementMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DestroyMediaElement();
        });
    }

    /// <summary>
    /// Destroys the MediaElement by disconnecting handler, disposing resources, and clearing references.
    /// The MediaElement will be recreated automatically by GetMediaElementAsync() when needed for the next playlist.
    /// </summary>
    private void DestroyMediaElement()
    {
        // Use local lock to synchronize with GetMediaElement
        lock (_lockObject)
        {
            try
            {
                // First, detach MediaElement from UI container (iOS/Windows) before disposing
                // This ensures proper cleanup order
                if (_mediaElementInstance != null)
                {
#if !ANDROID
                    // Detach from BootstrapPage container if attached (iOS/Windows only)
                    var bootstrapPage = _navigationService.GetBootstrapPage(shouldRetry: false);
                    if (bootstrapPage != null)
                    {
                        var mediaElementContainer = bootstrapPage.MediaElementContainerInstance;
                        if (mediaElementContainer != null && mediaElementContainer.Content == _mediaElementInstance)
                        {
                            mediaElementContainer.Content = null;
                            _logger.Information("MediaElement detached from BootstrapPage container");
                        }
                    }
#endif
                }

                // Now dispose the stored MediaElement instance
                if (_mediaElementInstance != null)
                {
                    _logger.Information("Disposing MediaElement instance");
                    
                    // Clear handler reference before disposing MediaElement
                    // This ensures handler is properly cleaned up
                    // CRITICAL: Call Dispose() directly instead of DisconnectHandler() because
                    // DisconnectHandler() override may not be called in headless mode when PlatformView is null.
                    // Direct disposal bypasses the DisconnectHandler issue and ensures cleanup happens.
                    if (_mediaElementInstance.Handler != null)
                    {
                        try
                        {
                            if (_mediaElementInstance.Handler is IDisposable disposableHandler)
                            {
                                // Prefer disconnect first to allow MediaElementHandler to detach Media3 listeners cleanly.
                                // Direct disposal here can lead to Media3 callbacks firing into disposed managed peers.
                                try
                                {
                                    _mediaElementInstance.Handler.DisconnectHandler();
                                    _logger.Debug("Handler disconnected via DisconnectHandler()");
                                }
                                catch (Exception ex)
                                {
                                    _logger.Debug(ex, "DisconnectHandler threw; continuing with handler disposal (best-effort)");
                                }
                                
                                disposableHandler.Dispose();
                                _logger.Debug("Handler disposed");
                            }
                            else
                            {
                                // Fallback to DisconnectHandler if handler doesn't implement IDisposable
                                _mediaElementInstance.Handler.DisconnectHandler();
                                _logger.Debug("Handler disconnected via DisconnectHandler()");
                            }
                            _mediaElementInstance.Handler = null;
                            
                            // Reset handler created flag so a new one can be created later
                            _handlerCreated = false;
                            _globalHandlerCreated = false; // Reset STATIC flag
                        }
                        catch (Exception handlerEx)
                        {
                            _logger.Warning(handlerEx, "Error disposing handler during MediaElement disposal");
                        }
                    }
                    
                    if (_mediaElementInstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _mediaElementInstance = null;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash - MediaElement will be recreated when needed
                _logger.Error(ex, "Error disposing MediaElement");
            }
        }
    }

    private bool _isDisposed;
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<DestroyMediaElementMessage>(this);
        
        // All injected services (_navigationService) are singletons, so don't dispose them
    }
}

