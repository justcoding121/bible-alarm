#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Serilog;
using System.IO;
#if IOS
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.Media;

public partial class AudioPlayer : IAudioPlayer, IRecipient<RecreateMediaElementMessage>
{
    private readonly ILogger _logger;
    private readonly IMediaElementService _mediaElementService;
    private readonly IDisplayMetadataService _displayMetadataService;
    private readonly IDispatcher _dispatcher;
    private readonly System.Timers.Timer? _positionTimer;
#if ANDROID
    private readonly IAndroidPlayerNotificationService? _androidPlayerNotificationService;
#endif

    private AudioPlayerTrack? _currentTrack;
    private TaskCompletionSource<bool>? _mediaOpenedCompletionSource;
    private bool _isResetting = false;
    private TimeSpan _lastDuration = TimeSpan.Zero;
    // MediaElement instance - populated in PrepareAsync
    private MediaElement? _mediaElement;

    public TimeSpan? CurrentPosition => _mediaElement?.Position;
    public TimeSpan Duration => _mediaElement?.Duration ?? TimeSpan.Zero;
    public PlayStatus Status { get; private set; } = PlayStatus.Stopped;
    
    /// <summary>
    /// Gets the actual current state of the MediaElement, not just the cached Status
    /// This checks the MediaElement's CurrentState property directly
    /// </summary>
    public bool IsActuallyPlayingOrPaused
    {
        get
        {
            // If we're resetting, don't check the actual state (it might be in transition)
            if (_isResetting)
            {
                return false;
            }
            
            // If source is null, MediaElement is not playing anything
            if (_mediaElement?.Source == null)
            {
                return false;
            }
            
            // CurrentState should be accessed on main thread, but for a simple check we'll do it directly
            // If this causes issues, we may need to make this async
            var actualState = _mediaElement.CurrentState;
            
            // Only return true if actually playing, paused, or buffering
            // If it's Stopped, None, Opening, or Failed, return false
            return actualState == MediaElementState.Playing || 
                   actualState == MediaElementState.Paused || 
                   actualState == MediaElementState.Buffering;
        }
    }

    public event EventHandler<EventArgs>? MediaEnded;
    public event EventHandler<EventArgs>? MediaFailed;

    public AudioPlayer(ILogger logger, IMediaElementService mediaElementService, IDisplayMetadataService displayMetadataService, IDispatcher dispatcher
#if ANDROID
        , IAndroidPlayerNotificationService? androidPlayerNotificationService = null
#endif
        )
    {
        _logger = logger;
        _mediaElementService = mediaElementService;
        _displayMetadataService = displayMetadataService;
        _dispatcher = dispatcher;
#if ANDROID
        _androidPlayerNotificationService = androidPlayerNotificationService;
#endif

        // MediaElement will be initialized lazily when first accessed
        // Event handlers will be attached in PrepareAsync

        // Register for RecreateMediaElementMessage to unsubscribe when MediaElement is recreated
        WeakReferenceMessenger.Default.Register<RecreateMediaElementMessage>(this);

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += OnPositionTimerElapsed;
        _positionTimer.AutoReset = true;
    }

    public async Task PrepareAsync(AudioPlayerTrack track, bool isFirstTrack = false, bool isLastTrack = false)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrEmpty(track.Uri))
            throw new ArgumentException("Track URI cannot be null or empty", nameof(track));

        // Get MediaElement from service if it's null, or if it was recreated
        var newMediaElement = _mediaElementService.GetMediaElement();
        
        // If MediaElement already exists and is different, unsubscribe from the old one
        if (_mediaElement != null && _mediaElement != newMediaElement)
        {
            _logger.Information("MediaElement instance changed - unsubscribing from old instance");
            UnsubscribeFromMediaElement(_mediaElement);
        }
        
        // Update reference and attach event handlers
        _mediaElement = newMediaElement;
        
        // Attach event handlers to the MediaElement
        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaOpened += OnMediaOpened;
        _mediaElement.PositionChanged += OnPositionChanged;

        await SafeStopMediaElementAsync(clearSource: false);

        _currentTrack = track;
        Status = PlayStatus.Loading;
        _mediaOpenedCompletionSource = new TaskCompletionSource<bool>();
        // Reset duration tracking so new track's duration will be detected as changed
        _lastDuration = TimeSpan.Zero;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Clear previous artwork to prevent iOS from trying to load null data
            _mediaElement.MetadataArtworkUrl = null!;
            
#if IOS
            var processedUri = iOSMediaElementHelper.ProcessUriForMediaElement(track.Uri, _logger);
            iOSMediaElementHelper.SetSourceAndVolume(_mediaElement, processedUri, _logger);
#elif ANDROID
            // CRITICAL: We MUST set a valid Source on MediaElement itself
            // even when using ExoPlayer directly for the queue.
            // Otherwise MediaElement stays in State.None forever.
            _mediaElement.Source = MediaSource.FromUri(track.Uri);
            
            // Now set the real (multi-item) queue via ExoPlayer to enable Next button
            // Pass MediaElement to the service with flags indicating track position
            _androidPlayerNotificationService?.SetSourceWithDummyQueue(_mediaElement, track.Uri, isFirstTrack, isLastTrack);
#else
            _mediaElement.Source = track.Uri;
#endif
        });

        // Wait for media to open (with timeout)
        // 5 second timeout
        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(_mediaOpenedCompletionSource.Task, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            _logger.Warning("Timeout waiting for media to open");
        }
    }

    public async Task PlayAsync()
    {
#if IOS
        await iOSMediaElementHelper.ConfigureAudioSessionBeforePlayAsync(_logger);
        var currentState = await iOSMediaElementHelper.GetCurrentStateAsync(_mediaElement, _logger);
        await iOSMediaElementHelper.HandlePausedStateAsync(_mediaElement, currentState, _logger);
        await iOSMediaElementHelper.InvokePlayAsync(_mediaElement, _logger);

        // Wait briefly and check if playback started
        await Task.Delay(100);

        var stateAfterPlay = await iOSMediaElementHelper.GetCurrentStateAsync(_mediaElement, _logger);
        // Check state using string comparison since helper returns object
        var stateString = stateAfterPlay.ToString();
        if (stateString == "Playing" || stateString == "Buffering")
        {
            _logger.Debug("MediaElement is in {State} state after Play()", stateAfterPlay);
        }
        await iOSMediaElementHelper.RetryPlayIfNeededAsync(_mediaElement, stateAfterPlay, _logger);
#else
        await InvokePlayOnMainThreadAsync();
        
        // Wait briefly and check if playback started
        await Task.Delay(100);
        
        var stateAfterPlay = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.CurrentState ?? MediaElementState.None);
        _logger.Debug("After Play() call, MediaElement state: {State}", stateAfterPlay);
#endif
    }

    /// <summary>
    /// Invokes Play() on the main thread (for non-iOS platforms).
    /// </summary>
    private async Task InvokePlayOnMainThreadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _logger.Debug("About to call MediaElement.Play(). Current state: {State}, Source: {Source}", 
                _mediaElement?.CurrentState ?? MediaElementState.None, 
                _mediaElement?.Source?.ToString() ?? "null");
            
            _mediaElement?.Play();
        });
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_currentTrack == null) return;

        // Signal that media is ready
        _mediaOpenedCompletionSource?.TrySetResult(true);

        // Update duration when track opens (track change)
        var currentDuration = Duration;
        if (currentDuration != _lastDuration && currentDuration > TimeSpan.Zero)
        {
            _lastDuration = currentDuration;
            _dispatcher.Dispatch(new PlaybackDurationChangedAction
            {
                Duration = currentDuration
            });
        }

        try
        {
            var metadata = await _displayMetadataService.GetDisplayMetadataAsync(_currentTrack);
            await ApplyMetadataToMediaElement(metadata);
            SendMetadataMessage(metadata);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Metadata extraction failed");
            var fallbackMeta = new MetaData
            {
                Title = "Unknown Title",
                Artist = "Unknown Artist"
            };
            await ApplyMetadataToMediaElement(fallbackMeta);
            SendMetadataMessage(fallbackMeta);
        }
    }

    private void SendMetadataMessage(MetaData meta)
    {
        // Dispatch Fluxor action
        _dispatcher.Dispatch(new PlaybackMetadataChangedAction
        {
            Title = meta.Title,
            Artist = meta.Artist,
            Album = meta.Album,
            ArtworkUrl = meta.ArtworkUrl
        });
    }

    private void SendPositionUpdate()
    {
        // Send position update via MVVM messaging (high-frequency updates)
        WeakReferenceMessenger.Default.Send(new PlaybackPositionChangedMessage
        {
            CurrentPosition = CurrentPosition
        });
        
        // Check if duration changed (track change) and update Fluxor state if needed
        var currentDuration = Duration;
        if (currentDuration != _lastDuration && currentDuration > TimeSpan.Zero)
        {
            _lastDuration = currentDuration;
            _dispatcher.Dispatch(new PlaybackDurationChangedAction
            {
                Duration = currentDuration
            });
        }
    }


    private async Task ApplyMetadataToMediaElement(MetaData meta)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _mediaElement.MetadataTitle = meta.Title ?? "";
            _mediaElement.MetadataArtist = meta.Artist ?? "";

            if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
            {
                var artworkPath = Path.Combine(FileSystem.CacheDirectory, "current_artwork.jpg");
                System.IO.File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
                _mediaElement.MetadataArtworkUrl = artworkPath;
            }
            else if (!string.IsNullOrEmpty(meta.ArtworkUrl))
            {
                _mediaElement.MetadataArtworkUrl = meta.ArtworkUrl;
            }
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Status = PlayStatus.Ended;
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnMediaFailed(object? sender, EventArgs e)
    {
        Status = PlayStatus.Failed;
        _mediaOpenedCompletionSource?.TrySetResult(false);
        
        var trackUri = _currentTrack?.Uri ?? "Unknown";
        _logger.Error("MediaElement failed to play track. URI: {TrackUri}, Source: {Source}", 
            trackUri, 
            _mediaElement?.Source?.ToString() ?? "null");
        
        MediaFailed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        // Don't update status if we're in the middle of resetting
        // This prevents race conditions where state changes fire after ResetAsync sets status to Stopped
        if (_isResetting && e.NewState != MediaElementState.Stopped)
        {
            _logger.Debug("Ignoring state change to {NewState} during reset", e.NewState);
            return;
        }

        // On iOS, MediaElement can fire state change events even after Source is set to null
        // If Source is null, we should ignore state changes (except Stopped/None) to prevent stale status updates
        // Also, if Source is null, force status to Stopped regardless of the state change
        if (_mediaElement?.Source == null)
        {
            if (e.NewState != MediaElementState.Stopped && e.NewState != MediaElementState.None)
            {
                _logger.Debug("Ignoring state change to {NewState} because Source is null, forcing Status to Stopped", e.NewState);
                Status = PlayStatus.Stopped;
                SendStatusMessage();
                _positionTimer?.Stop();
                return;
            }
        }

        Status = e.NewState switch
        {
            MediaElementState.Playing => PlayStatus.Playing,
            MediaElementState.Paused => PlayStatus.Paused,
            MediaElementState.Stopped => PlayStatus.Stopped,
            MediaElementState.Buffering => PlayStatus.Loading,
            MediaElementState.Failed => PlayStatus.Failed,
            // None is equivalent to Stopped
            MediaElementState.None => PlayStatus.Stopped,
            _ => PlayStatus.Stopped
        };

        SendStatusMessage();

        if (Status == PlayStatus.Playing)
        {
            _logger.Debug("Status changed to Playing, starting position timer. CurrentState: {CurrentState}", e.NewState);
            _positionTimer?.Start();
        }
        else
        {
            _logger.Debug("Status changed to {Status}, stopping position timer. CurrentState: {CurrentState}", Status, e.NewState);
            _positionTimer?.Stop();
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        SendPositionUpdate();
    }

    private void OnPositionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Check position on main thread since MediaElement properties must be accessed on UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SendPositionUpdate();
        });
    }

    private void SendStatusMessage()
    {
        // Dispatch status change to Fluxor state
        _dispatcher.Dispatch(new PlaybackStatusChangedAction(Status));
    }

    public Task PauseAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.Pause());
    }

    public Task ResumeAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.Play());
    }

    public Task StopAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.Stop());
    }

    public async Task ResetAsync()
    {
        _isResetting = true;
        
        try
        {
            // Stop and clear source
            await SafeStopMediaElementAsync(clearSource: true);
            
            // Wait a bit for the stop to take effect
            await Task.Delay(100);
            
            // Force stop again if still in a valid state to stop
            var actualState = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.CurrentState ?? MediaElementState.None);
            if (actualState == MediaElementState.Playing || 
                actualState == MediaElementState.Paused || 
                actualState == MediaElementState.Buffering)
            {
                _logger.Debug("MediaElement still in {State} state after first stop, forcing stop again", actualState);
                await SafeStopMediaElementAsync(clearSource: true);
                await Task.Delay(100);
            }
            
            // Ensure status is reset to Stopped
            Status = PlayStatus.Stopped;
            _currentTrack = null;
            _positionTimer?.Stop();
            _mediaOpenedCompletionSource?.TrySetCanceled();
            _mediaOpenedCompletionSource = null;
            SendStatusMessage();
            
#if ANDROID
            // Release MediaSession to hide the media notification
            // This is the ONLY place where we call ReleaseMediaSession - only on final stop, not during track changes
            // Add a small delay to ensure MediaElement has fully stopped before releasing
            _logger.Information("Resetting playback - calling ReleaseMediaSession to remove notification");
            // Small delay to let MediaElement finish stopping
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_mediaElement != null)
                {
                    _androidPlayerNotificationService?.ReleaseMediaSession(_mediaElement);
                }
            });
            _logger.Information("ReleaseMediaSession called - notification should be removed");
#endif
            
            
            // Verify final state
            actualState = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.CurrentState ?? MediaElementState.None);
            _logger.Debug("Reset completed. MediaElement state: {State}, Status: {Status}", actualState, Status);
        }
        finally
        {
            _isResetting = false;
        }
    }

    public Task SeekToAsync(TimeSpan position)
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement?.SeekTo(position));
    }


    /// <summary>
    /// Safely stops the MediaElement by checking its state first.
    /// On Android, calling Stop() when in IDLE or ERROR states causes errors.
    /// </summary>
    /// <param name="clearSource">If true, clears the Source property after stopping. If false, only clears Source for invalid states.</param>
    private Task SafeStopMediaElementAsync(bool clearSource = true)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_mediaElement == null) return;
            
            // Only call Stop() if MediaElement is in a valid state (Playing, Paused, or Buffering)
            // On Android, calling Stop() when in IDLE or ERROR states causes errors
            var currentState = _mediaElement.CurrentState;
            if (currentState == MediaElementState.Playing || 
                currentState == MediaElementState.Paused || 
                currentState == MediaElementState.Buffering)
            {
                _mediaElement.Stop();
            }
            
            // Clear source if requested, or if in an invalid state (like Failed)
            if (clearSource || (currentState != MediaElementState.Stopped && currentState != MediaElementState.None))
            {
                _mediaElement.Source = null;
            }
        });
    }


    public void Dispose()
    {
        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<RecreateMediaElementMessage>(this);

        if (_positionTimer != null)
        {
            _positionTimer.Elapsed -= OnPositionTimerElapsed;
            _positionTimer.Stop();
            _positionTimer.Dispose();
        }

        // Unsubscribe from current MediaElement if it exists
        if (_mediaElement != null)
        {
            UnsubscribeFromMediaElement(_mediaElement);
        }
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles RecreateMediaElementMessage by unsubscribing from the old MediaElement.
    /// This is called when BootstrapPage disposes and recreates the MediaElement.
    /// </summary>
    public void Receive(RecreateMediaElementMessage message)
    {
        if (_mediaElement != null)
        {
            _logger.Information("Received RecreateMediaElementMessage - unsubscribing from old MediaElement");
            UnsubscribeFromMediaElement(_mediaElement);
            // Clear reference since MediaElement is being recreated
            _mediaElement = null;
        }
    }

    /// <summary>
    /// Unsubscribes from all events on the specified MediaElement.
    /// </summary>
    private void UnsubscribeFromMediaElement(MediaElement mediaElement)
    {
        try
        {
            mediaElement.StateChanged -= OnStateChanged;
            mediaElement.MediaOpened -= OnMediaOpened;
            mediaElement.MediaEnded -= OnMediaEnded;
            mediaElement.MediaFailed -= OnMediaFailed;
            mediaElement.PositionChanged -= OnPositionChanged;
            _logger.Debug("Unsubscribed from MediaElement events");
        }
        catch (Exception ex)
        {
            // MediaElement may have been disposed, ignore
            _logger.Debug(ex, "Error unsubscribing from MediaElement events (may have been disposed)");
        }
    }
}
