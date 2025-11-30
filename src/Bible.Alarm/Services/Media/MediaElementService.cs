#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Views.General;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Service for managing and accessing the MediaElement instance.
/// Handles creation and retrieval of MediaElement from BootstrapPage.
/// Thread-safe: ensures MediaElement creation happens on the main thread to prevent deadlocks and crashes.
/// Also handles MediaElement disposal and recreation when RecreateMediaElementMessage is received.
/// </summary>
public class MediaElementService : IMediaElementService, IRecipient<RecreateMediaElementMessage>, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;
    // Local lock for this service
    private readonly object _lockObject = new object();

    public MediaElementService(INavigationService navigationService, ILogger logger)
    {
        _navigationService = navigationService;
        _logger = logger;
        
        // Register for RecreateMediaElementMessage to handle MediaElement disposal
        WeakReferenceMessenger.Default.Register<RecreateMediaElementMessage>(this);
    }

    public MediaElement GetMediaElement()
    {
        var bootstrapPage = _navigationService.GetBootstrapPage();
        if (bootstrapPage == null)
        {
            _logger.Error("BootstrapPage not found in navigation stack - this should never happen!");
            throw new InvalidOperationException("BootstrapPage not found in navigation stack. BootstrapPage must be loaded before accessing MediaElement.");
        }

        // Use local lock to synchronize access to MediaElement
        lock (_lockObject)
        {
            var mediaElementContainer = bootstrapPage.MediaElementContainerInstance;
            if (mediaElementContainer?.Content is MediaElement mediaElement)
            {
                _logger.Debug("MediaElement found in BootstrapPage");
                return mediaElement;
            }

            // MediaElement doesn't exist (was disposed), create a new one
            // MUST be done on main thread - MediaElement creation and Content assignment must be on UI thread
            _logger.Information("MediaElement not found in BootstrapPage, creating new instance on main thread");
            
            MediaElement? newMediaElement = null;
            
            if (MainThread.IsMainThread)
            {
                newMediaElement = CreateMediaElementOnMainThread(bootstrapPage);
            }
            else
            {
                // Synchronously invoke on main thread to ensure MediaElement is created before returning
                // This prevents race conditions where MediaElement is accessed before it's fully created
                MainThread.InvokeOnMainThreadAsync(() =>
                {
                    newMediaElement = CreateMediaElementOnMainThread(bootstrapPage);
                }).GetAwaiter().GetResult();
            }

            if (newMediaElement == null)
            {
                _logger.Error("Failed to create MediaElement on main thread");
                throw new InvalidOperationException("Failed to create MediaElement - could not create on main thread");
            }

            _logger.Information("New MediaElement created and added to BootstrapPage");
            return newMediaElement;
        }
    }

    private MediaElement CreateMediaElementOnMainThread(BootstrapPage bootstrapPage)
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

        // Add to the container - this must be on main thread
        bootstrapPage.MediaElementContainerInstance.Content = newMediaElement;

        return newMediaElement;
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
    /// The MediaElement will be recreated by GetMediaElement() when needed.
    /// </summary>
    private void ReplaceMediaElement()
    {
        // Use local lock to synchronize with GetMediaElement
        lock (_lockObject)
        {
            try
            {
                var bootstrapPage = _navigationService.GetBootstrapPage();
                if (bootstrapPage == null)
                {
                    _logger.Warning("BootstrapPage not found when trying to dispose MediaElement");
                    return;
                }

                var mediaElementContainer = bootstrapPage.MediaElementContainerInstance;
                var oldMediaElement = mediaElementContainer?.Content as MediaElement;
                
                if (oldMediaElement != null)
                {
                    _logger.Information("Disposing old MediaElement instance");
                    // Dispose the old MediaElement
                    if (oldMediaElement is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                // Set content to null - MediaElement will be recreated by GetMediaElement() when needed
                if (mediaElementContainer != null)
                {
                    mediaElementContainer.Content = null;
                    _logger.Information("MediaElement container content set to null - will be recreated on next GetMediaElement() call");
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash - MediaElement will be recreated when needed
                _logger.Error(ex, "Error disposing MediaElement");
            }
        }
    }

    public void Dispose()
    {
        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<RecreateMediaElementMessage>(this);
    }
}

