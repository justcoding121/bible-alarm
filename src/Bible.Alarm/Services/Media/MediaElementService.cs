#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Handles creation and retrieval of MediaElement from BootstrapPage.
/// Thread-safe: ensures MediaElement creation happens on the main thread to prevent deadlocks and crashes.
/// Also handles MediaElement disposal and recreation when RecreateMediaElementMessage is received.
/// MediaElement can exist without being attached to BootstrapPage container (e.g., when app is backgrounded).
/// When BootstrapPage becomes available, MediaElement will be reattached automatically.
/// </summary>
public class MediaElementService : IMediaElementService, IRecipient<RecreateMediaElementMessage>, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;
    // Local lock for this service
    private readonly object _lockObject = new object();
    // Store MediaElement instance independently of BootstrapPage container
    // This allows MediaElement to exist when app is backgrounded (no UI)
    private MediaElement? _mediaElementInstance;

    public MediaElementService(INavigationService navigationService, ILogger logger)
    {
        _navigationService = navigationService;
        _logger = logger;
        
        // Register for RecreateMediaElementMessage to handle MediaElement disposal
        WeakReferenceMessenger.Default.Register<RecreateMediaElementMessage>(this);
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
            
            // Try to attach to BootstrapPage if it's available
            TryAttachToBootstrapPage(existingInstance);
            
            return existingInstance;
        }

        // MediaElement doesn't exist, create a new one
        // MUST be done on main thread - MediaElement creation must be on UI thread
        _logger.Information("MediaElement not found, creating new instance on main thread");
        
        MediaElement? newMediaElement = null;
        
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

        // Store the instance
        lock (_lockObject)
        {
            _mediaElementInstance = newMediaElement;
        }
        
        // Try to attach to BootstrapPage if it's available
        TryAttachToBootstrapPage(newMediaElement);
        
        _logger.Information("New MediaElement created and stored in service");
        return newMediaElement;
    }

    /// <summary>
    /// Creates a new MediaElement instance on the main thread.
    /// Does not attach to BootstrapPage - that's handled separately by TryAttachToBootstrapPage.
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

    /// <summary>
    /// Tries to attach MediaElement to BootstrapPage container if BootstrapPage is available.
    /// This allows MediaElement to work when app is backgrounded (no UI) and reattach when UI is recreated.
    /// Non-blocking: Passes shouldRetry=false to prevent indefinite blocking when navigation is not available.
    /// Safe from circular calls: Checks if already attached before attaching to prevent infinite loops.
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

            // Only attach if not already attached - prevents re-attachment and potential circular calls
            if (mediaElementContainer.Content != mediaElement)
            {
                _logger.Debug("Attaching MediaElement to BootstrapPage container");
                mediaElementContainer.Content = mediaElement;
            }
            else
            {
                _logger.Debug("MediaElement already attached to BootstrapPage container - skipping");
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - MediaElement can work without UI container
            _logger.Warning(ex, "Failed to attach MediaElement to BootstrapPage - MediaElement will work without UI container");
        }
    }

    /// <summary>
    /// Public method to reattach MediaElement when BootstrapPage becomes available.
    /// Called from BootstrapPage.OnAppearing when page is recreated.
    /// </summary>
    public void ReattachMediaElementIfNeeded()
    {
        lock (_lockObject)
        {
            if (_mediaElementInstance != null)
            {
                _logger.Information("Reattaching MediaElement to BootstrapPage container");
                TryAttachToBootstrapPage(_mediaElementInstance);
            }
        }
    }

    /// <summary>
    /// Handles RecreateMediaElementMessage by disposing the old MediaElement and setting container content to null.
    /// This is called after MediaSession release to ensure a clean ExoPlayer instance.
    /// </summary>
    public void Receive(RecreateMediaElementMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ReplaceMediaElement();
        });
    }

    /// <summary>
    /// Disposes the MediaElement and sets the container content to null.
    /// The MediaElement will be recreated by GetMediaElementAsync() when needed.
    /// </summary>
    private void ReplaceMediaElement()
    {
        // Use local lock to synchronize with GetMediaElement
        lock (_lockObject)
        {
            try
            {
                // Dispose the stored MediaElement instance
                if (_mediaElementInstance != null)
                {
                    _logger.Information("Disposing MediaElement instance");
                    if (_mediaElementInstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _mediaElementInstance = null;
                }

                // Also clear BootstrapPage container if available
                // Use shouldRetry=false to avoid blocking when disposing (e.g., app is backgrounded)
                var bootstrapPage = _navigationService.GetBootstrapPage(shouldRetry: false);
                if (bootstrapPage != null)
                {
                    var mediaElementContainer = bootstrapPage.MediaElementContainerInstance;
                    if (mediaElementContainer != null)
                    {
                        mediaElementContainer.Content = null;
                        _logger.Information("MediaElement container content set to null");
                    }
                }
                else
                {
                    _logger.Debug("BootstrapPage not available when disposing MediaElement - instance already cleared");
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
        WeakReferenceMessenger.Default.Unregister<RecreateMediaElementMessage>(this);
        
        // All injected services (_navigationService) are singletons, so don't dispose them
    }
}

