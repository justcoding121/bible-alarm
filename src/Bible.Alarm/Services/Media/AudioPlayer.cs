#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if IOS
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.Media;

public sealed class AudioPlayer : IAudioPlayer, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaElementService mediaElementService;
    private readonly IDisplayMetadataService displayMetadataService;
    private readonly IDispatcher dispatcher;
#if ANDROID
    private readonly IAndroidPlayerNotificationService? androidPlayerNotificationService;
#endif

    private AudioPlayerTrack? currentTrack;
    private TaskCompletionSource<bool>? mediaOpenedCompletionSource;
    private bool isResetting;
    private TimeSpan lastDuration = TimeSpan.Zero;
    private bool isSeeking;
    private PlayStatus statusBeforeSeek = PlayStatus.Stopped;
    // Track when we're transitioning from Loading to Playing to ignore intermediate Paused states
    private bool isTransitioningFromLoadingToPlaying;
    private DateTime? lastLoadingToPlayingTransitionTime;
    // Throttle position updates to 500ms to reduce update frequency
    private DateTime? lastPositionUpdateTime;
    private const int PositionUpdateIntervalMs = 500;
    // MediaElement instance - populated in PrepareAsync
    private MediaElement? mediaElement;

    public TimeSpan? CurrentPosition => mediaElement?.Position;
    public TimeSpan Duration => mediaElement?.Duration ?? TimeSpan.Zero;
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
            if (isResetting)
            {
                return false;
            }

            // If source is null, MediaElement is not playing anything
            if (mediaElement?.Source == null)
            {
                return false;
            }

            // CurrentState should be accessed on main thread, but for a simple check we'll do it directly
            // If this causes issues, we may need to make this async
            var actualState = mediaElement.CurrentState;

            // Only return true if actually playing, paused, or buffering
            // If it's Stopped, None, Opening, or Failed, return false
            return actualState is MediaElementState.Playing or
                   MediaElementState.Paused or
                   MediaElementState.Buffering;
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
        this.logger = logger;
        this.mediaElementService = mediaElementService;
        this.displayMetadataService = displayMetadataService;
        this.dispatcher = dispatcher;
#if ANDROID
        this.androidPlayerNotificationService = androidPlayerNotificationService;
#endif

        // MediaElement will be initialized lazily when first accessed
        // Event handlers will be attached in PrepareAsync

        // MediaElement is now a singleton for all platforms - do not register for DestroyMediaElementMessage
        // MediaElement will live for the entire app process lifetime
    }

    public async Task PrepareAsync(AudioPlayerTrack track, bool isFirstTrack = false, bool isLastTrack = false)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrEmpty(track.Uri))
        {
            throw new ArgumentException("Track URI cannot be null or empty", nameof(track));
        }

        // Get MediaElement from service if it's null, or if it was recreated
        var newMediaElement = await mediaElementService.GetMediaElementAsync();

        // Always unsubscribe from current MediaElement before subscribing again
        // This prevents duplicate event handlers if PrepareAsync is called multiple times
        if (mediaElement != null)
        {
            if (mediaElement != newMediaElement)
            {
                logger.Information("MediaElement instance changed - unsubscribing from old instance");
            }
            UnsubscribeFromMediaElement(mediaElement);
        }

        // Update reference and attach event handlers
        mediaElement = newMediaElement;

        // Attach event handlers to the MediaElement
        mediaElement.StateChanged += OnStateChanged;
        mediaElement.MediaEnded += OnMediaEnded;
        mediaElement.MediaFailed += OnMediaFailed;
        mediaElement.MediaOpened += OnMediaOpened;
        mediaElement.PositionChanged += OnPositionChanged;
        mediaElement.SeekCompleted += OnSeekCompleted;

        await SafeStopMediaElementAsync(clearSource: false);

        currentTrack = track;
        Status = PlayStatus.Loading;
        mediaOpenedCompletionSource = new TaskCompletionSource<bool>();
        // Reset duration tracking so new track's duration will be detected as changed
        lastDuration = TimeSpan.Zero;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Clear previous artwork to prevent iOS from trying to load null data
            mediaElement.MetadataArtworkUrl = null!;

#if IOS
            var processedUri = IosMediaElementHelper.ProcessUriForMediaElement(track.Uri, logger);
            IosMediaElementHelper.SetSourceAndVolume(mediaElement, processedUri, logger);
#elif ANDROID
            // Handler is guaranteed to exist - GetMediaElementAsync() creates it for headless mode
            // CRITICAL: We MUST set a valid Source on MediaElement itself
            // even when using ExoPlayer directly for the queue.
            // Otherwise, MediaElement stays in State.None forever.
            mediaElement.Source = MediaSource.FromUri(track.Uri);

            // Now set the real (multi-item) queue via ExoPlayer to enable Next button
            // Pass MediaElement to the service with flags indicating track position
            androidPlayerNotificationService?.SetSourceWithDummyQueue(mediaElement, track.Uri, isFirstTrack, isLastTrack);
#else
            mediaElement.Source = track.Uri;
#endif
        });

        // Wait for media to open (with timeout)
        // 5 second timeout
        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(mediaOpenedCompletionSource.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            logger.Warning("Timeout waiting for media to open");
        }
    }

    public async Task PlayAsync()
    {
#if IOS
        if (mediaElement == null)
        {
            return;
        }

        await IosMediaElementHelper.ConfigureAudioSessionBeforePlayAsync(logger);
        var currentState = await IosMediaElementHelper.GetCurrentStateAsync(mediaElement, logger);
        await IosMediaElementHelper.HandlePausedStateAsync(mediaElement, currentState, logger);
        await IosMediaElementHelper.InvokePlayAsync(mediaElement, logger);

        // Wait briefly and check if playback started
        await Task.Delay(100);

        var stateAfterPlay = await IosMediaElementHelper.GetCurrentStateAsync(mediaElement, logger);
        // Check state using string comparison since helper returns object
        var stateString = stateAfterPlay.ToString();
        if (stateString is "Playing" or "Buffering")
        {
            logger.Debug("MediaElement is in {State} state after Play()", stateAfterPlay);
        }
        await IosMediaElementHelper.RetryPlayIfNeededAsync(mediaElement, stateAfterPlay, logger);
#else
        await InvokePlayOnMainThreadAsync();

        // Wait briefly and check if playback started
        await Task.Delay(100);

        var stateAfterPlay = await MainThread.InvokeOnMainThreadAsync(() => mediaElement?.CurrentState ?? MediaElementState.None);
        logger.Debug("After Play() call, MediaElement state: {State}", stateAfterPlay);
#endif
    }

    /// <summary>
    /// Invokes Play() on the main thread (for non-iOS platforms).
    /// </summary>
    private async Task InvokePlayOnMainThreadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            logger.Debug("About to call MediaElement.Play(). Current state: {State}, Source: {Source}",
                mediaElement?.CurrentState ?? MediaElementState.None,
                mediaElement?.Source?.ToString() ?? "null");

            mediaElement?.Play();
        });
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        if (currentTrack == null)
        {
            return;
        }

        // Signal that media is ready
        mediaOpenedCompletionSource?.TrySetResult(true);

        // Update duration when track opens (track change)
        var currentDuration = Duration;
        if (currentDuration != lastDuration && currentDuration > TimeSpan.Zero)
        {
            lastDuration = currentDuration;
            dispatcher.Dispatch(new PlaybackDurationChangedAction
            {
                Duration = currentDuration
            });
        }

        try
        {
            var metadata = await displayMetadataService.GetDisplayMetadataAsync(currentTrack);
            await ApplyMetadataToMediaElement(metadata);
            SendMetadataMessage(metadata);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Metadata extraction failed");
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
        // If we have artwork bytes, save them to a file and use that path
        // Uses different filename than default schedule artwork to avoid conflicts
        string? artworkUrl = meta.ArtworkUrl;
        if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0 && string.IsNullOrEmpty(artworkUrl))
        {
            try
            {
                var artworkPath = Path.Combine(FileSystem.CacheDirectory, "playing_track_artwork.jpg");
                File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
                artworkUrl = artworkPath;
                logger.Debug($"Saved playing track artwork to {artworkPath}, size: {meta.ArtworkBytes.Length} bytes");
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to save playing track artwork to file");
            }
        }

        // Dispatch Fluxor action
        dispatcher.Dispatch(new PlaybackMetadataChangedAction
        {
            Title = meta.Title,
            Artist = meta.Artist,
            Album = meta.Album,
            ArtworkUrl = artworkUrl
        });

        if (!string.IsNullOrEmpty(artworkUrl))
        {
            logger.Debug($"Dispatched metadata with ArtworkUrl: {artworkUrl}");
        }
        else
        {
            logger.Debug("Dispatched metadata without ArtworkUrl");
        }
    }

    private void SendPositionUpdate()
    {
        // Throttle position updates to 500ms to reduce update frequency
        // MediaElement's PositionChanged event fires every ~200ms, but we only need updates every 500ms
        var now = DateTime.UtcNow;
        if (lastPositionUpdateTime.HasValue)
        {
            var timeSinceLastUpdate = (now - lastPositionUpdateTime.Value).TotalMilliseconds;
            if (timeSinceLastUpdate < PositionUpdateIntervalMs)
            {
                // Skip this update - not enough time has passed
                return;
            }
        }

        // Update the last update time
        lastPositionUpdateTime = now;

        // Send position update via MVVM messaging (throttled to 500ms)
        WeakReferenceMessenger.Default.Send(new PlaybackPositionChangedMessage
        {
            CurrentPosition = CurrentPosition
        });

        // Check if duration changed (track change) and update Fluxor state if needed
        var currentDuration = Duration;
        if (currentDuration != lastDuration && currentDuration > TimeSpan.Zero)
        {
            lastDuration = currentDuration;
            dispatcher.Dispatch(new PlaybackDurationChangedAction
            {
                Duration = currentDuration
            });
        }
    }


    private async Task ApplyMetadataToMediaElement(MetaData meta)
    {
        var mediaElement = this.mediaElement;
        if (mediaElement == null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            mediaElement.MetadataTitle = meta.Title ?? "";
            mediaElement.MetadataArtist = meta.Artist ?? "";

            if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
            {
                // Use same filename as SendMetadataMessage for consistency
                var artworkPath = Path.Combine(FileSystem.CacheDirectory, "playing_track_artwork.jpg");
                File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
                mediaElement.MetadataArtworkUrl = artworkPath;
            }
            else if (!string.IsNullOrEmpty(meta.ArtworkUrl))
            {
                mediaElement.MetadataArtworkUrl = meta.ArtworkUrl;
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
        mediaOpenedCompletionSource?.TrySetResult(false);

        var trackUri = currentTrack?.Uri ?? "Unknown";
        logger.Error("MediaElement failed to play track. URI: {TrackUri}, Source: {Source}",
            trackUri,
            mediaElement?.Source?.ToString() ?? "null");

        MediaFailed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        // Don't update status if we're in the middle of resetting
        // This prevents race conditions where state changes fire after ResetAsync sets status to Stopped
        if (isResetting && e.NewState != MediaElementState.Stopped)
        {
            logger.Debug("Ignoring state change to {NewState} during reset", e.NewState);
            return;
        }

        // Ignore Stopped state changes when we're in Loading state (preparing a new track)
        // This prevents the intermediate Stopped state from SafeStopMediaElementAsync during PrepareAsync
        // from causing Android Auto controls to flicker (play => stop => play => pause)
        if (e.NewState == MediaElementState.Stopped && Status == PlayStatus.Loading)
        {
            logger.Debug("Ignoring Stopped state change during Loading (preparing new track)");
            return;
        }

        // Ignore intermediate Paused states when transitioning from Loading/Buffering to Playing
        // MediaElement briefly goes through Paused state when transitioning from Buffering to Playing,
        // which causes rapid button flicker in Android Auto. We'll ignore Paused if we were just in Loading
        // and mark that we're in a transition. If Playing follows within 200ms, we'll clear the flag.
        // If we get another Paused after the window, it's a real pause.
        if (e.NewState == MediaElementState.Paused)
        {
            if (Status == PlayStatus.Loading)
            {
                // First Paused state after Loading - mark transition and ignore it
                isTransitioningFromLoadingToPlaying = true;
                lastLoadingToPlayingTransitionTime = DateTime.UtcNow;
                logger.Debug("Ignoring intermediate Paused state during Loading->Playing transition (preventing button flicker)");
                return;
            }
            else if (isTransitioningFromLoadingToPlaying)
            {
                // We're in a transition and got another Paused state
                var timeSinceTransition = lastLoadingToPlayingTransitionTime.HasValue
                    ? (DateTime.UtcNow - lastLoadingToPlayingTransitionTime.Value).TotalMilliseconds
                    : double.MaxValue;

                // If more than 200ms has passed since the transition started, treat this as a real pause
                if (timeSinceTransition > 200)
                {
                    isTransitioningFromLoadingToPlaying = false;
                    lastLoadingToPlayingTransitionTime = null;
                    logger.Debug("Paused state received after transition window ({Time}ms), treating as real pause", timeSinceTransition);
                    // Continue to process this Paused state normally
                }
                else
                {
                    logger.Debug("Ignoring Paused state during Loading->Playing transition ({Time}ms since transition start)", timeSinceTransition);
                    return;
                }
            }
        }

        // If we're transitioning from Loading to Playing and we get a Playing state,
        // clear the transition flag
        if (e.NewState == MediaElementState.Playing && isTransitioningFromLoadingToPlaying)
        {
            isTransitioningFromLoadingToPlaying = false;
            lastLoadingToPlayingTransitionTime = null;
        }

        // On iOS, MediaElement can fire state change events even after Source is set to null
        // If Source is null, we should ignore state changes (except Stopped/None) to prevent stale status updates
        // Also, if Source is null, force status to Stopped regardless of the state change
        if (mediaElement?.Source == null)
        {
            if (e.NewState is not MediaElementState.Stopped and not MediaElementState.None)
            {
                logger.Debug("Ignoring state change to {NewState} because Source is null, forcing Status to Stopped", e.NewState);
                Status = PlayStatus.Stopped;
                SendStatusMessage();
                return;
            }
        }

        // During seeking, preserve the previous status to prevent flickering
        // MediaElement may transition to Buffering during seek, but we want to keep showing
        // the correct play/pause button state
        if (isSeeking && e.NewState == MediaElementState.Buffering)
        {
            // Preserve the status we had before seeking started
            Status = statusBeforeSeek;
            logger.Debug("Ignoring Buffering state change during seek, preserving status: {Status}", Status);
        }
        else
        {
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
        }

        SendStatusMessage();
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        // MediaElement's PositionChanged event fires when position updates
        // MediaElement has its own internal timer (200ms) that triggers this event
        // Removed verbose logging to reduce CPU usage - only log if seeking
        if (isSeeking)
        {
            logger.Debug("[AudioPlayer] OnPositionChanged during seek - CurrentPosition: {Position}",
                CurrentPosition?.ToString() ?? "null");
        }
        SendPositionUpdate();
    }

    private void SendStatusMessage()
    {
        // Dispatch status change to Fluxor state
        logger.Debug(
            "[AudioPlayer] SendStatusMessage: Dispatching PlaybackStatusChangedAction - Status={Status}, CurrentPosition={CurrentPosition}ms, Duration={Duration}ms",
            Status,
            (long)(CurrentPosition?.TotalMilliseconds ?? 0),
            (long)Duration.TotalMilliseconds);
        dispatcher.Dispatch(new PlaybackStatusChangedAction(Status));
    }

    public Task PauseAsync() => MainThread.InvokeOnMainThreadAsync(() => mediaElement?.Pause());

    public Task ResumeAsync() => MainThread.InvokeOnMainThreadAsync(() => mediaElement?.Play());

    public Task StopAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (mediaElement == null)
            {
                return;
            }

            try
            {
                mediaElement.Stop();
            }
            catch (InvalidOperationException ex)
            {
                // On iOS, Stop() internally tries to seek to zero, which can fail if the player isn't ready
                // Log and continue - the player will be in a stopped state anyway
                logger.Debug(ex, "Stop() failed because player isn't ready to seek, but player should be stopped");
            }
            catch (Exception ex)
            {
                // Catch any other exceptions
                logger.Warning(ex, "Exception occurred while stopping MediaElement");
            }
        });
    }

    public async Task ResetAsync()
    {
        isResetting = true;

        try
        {
            // Stop and clear source
            await SafeStopMediaElementAsync(clearSource: true);

            // Wait a bit for the stop to take effect
            await Task.Delay(100);

            // Force stop again if still in a valid state to stop
            var actualState = await MainThread.InvokeOnMainThreadAsync(() => mediaElement?.CurrentState ?? MediaElementState.None);
            if (actualState is MediaElementState.Playing or
                MediaElementState.Paused or
                MediaElementState.Buffering)
            {
                logger.Debug("MediaElement still in {State} state after first stop, forcing stop again", actualState);
                await SafeStopMediaElementAsync(clearSource: true);
                await Task.Delay(100);
            }

            // Ensure status is reset to Stopped
            Status = PlayStatus.Stopped;
            currentTrack = null;
            mediaOpenedCompletionSource?.TrySetCanceled();
            mediaOpenedCompletionSource = null;
            // Clear transition flag
            isTransitioningFromLoadingToPlaying = false;
            lastLoadingToPlayingTransitionTime = null;
            // Clear position update throttling
            lastPositionUpdateTime = null;
            SendStatusMessage();

#if ANDROID
            // Release MediaSession to hide the media notification
            // This is the ONLY place where we call ReleaseMediaSession - only on final stop, not during track changes
            // Add a small delay to ensure MediaElement has fully stopped before releasing
            logger.Information("Resetting playback - calling ReleaseMediaSession to remove notification");
            // Small delay to let MediaElement finish stopping
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (mediaElement != null)
                {
                    androidPlayerNotificationService?.ReleaseMediaSession(mediaElement);
                }
            });
            logger.Information("ReleaseMediaSession called - notification should be removed");

            // Android: MediaElement is now a singleton for app lifetime - do not clear the reference
            // The MediaElement instance remains alive, just the notification is removed
            // No need to wait for DestroyMediaElementMessage (it's not sent anymore)
            logger.Debug("MediaElement instance remains alive for app lifetime");
#endif


            // Verify final state
            actualState = await MainThread.InvokeOnMainThreadAsync(() => mediaElement?.CurrentState ?? MediaElementState.None);
            logger.Debug("Reset completed. MediaElement state: {State}, Status: {Status}", actualState, Status);
        }
        finally
        {
            isResetting = false;
        }
    }

    public async Task SeekToAsync(TimeSpan position)
    {
        // Track that we're seeking to prevent state flickering during seek
        isSeeking = true;
        statusBeforeSeek = Status;

        logger.Debug("[AudioPlayer] SeekToAsync called - Position: {Position}, StatusBeforeSeek: {Status}, Setting _isSeeking = true",
            position, statusBeforeSeek);

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (mediaElement == null)
                {
                    logger.Warning("[AudioPlayer] MediaElement is null, cannot seek");
                    isSeeking = false;
                    return;
                }

                logger.Debug("[AudioPlayer] About to call MediaElement.SeekTo({Position})", position);

                try
                {
                    // Start fallback timer to reset seeking flag if SeekCompleted doesn't fire
                    _ = ResetSeekingFlagWithTimeoutAsync();

                    // Await the seek operation - it will complete when SeekCompleted event fires
                    await mediaElement.SeekTo(position);
                    logger.Debug("[AudioPlayer] MediaElement.SeekTo() completed successfully");
                    // Note: _isSeeking will be reset in OnSeekCompleted when seek actually finishes
                }
                catch (InvalidOperationException ex)
                {
                    // Seek failed (e.g., position outside seekable ranges, player not ready)
                    logger.Warning(ex, "[AudioPlayer] SeekTo failed - {Message}", ex.Message);
                    isSeeking = false;
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[AudioPlayer] Error during SeekToAsync");
            isSeeking = false;
        }
    }

    private void OnSeekCompleted(object? sender, EventArgs e)
    {
        // Seek has completed - reset the seeking flag
        // This allows position updates to resume and status changes to be processed normally
        var currentPosition = CurrentPosition;
        logger.Debug("[AudioPlayer] OnSeekCompleted event fired - CurrentPosition: {Position}, Resetting _isSeeking = false",
            currentPosition?.ToString() ?? "null");
        isSeeking = false;
        logger.Debug("[AudioPlayer] Seek completed, resuming normal position and status updates. _isSeeking: {IsSeeking}", isSeeking);
    }

    private async Task ResetSeekingFlagWithTimeoutAsync()
    {
        // Fallback: If SeekCompleted doesn't fire within 3 seconds, reset the flag anyway
        await Task.Delay(3000);
        if (isSeeking)
        {
            logger.Warning("[AudioPlayer] SeekCompleted event did not fire within 3 seconds - resetting _isSeeking flag as fallback");
            isSeeking = false;
        }
    }


    /// <summary>
    /// Safely stops the MediaElement by checking its state first.
    /// On Android, calling Stop() when in IDLE or ERROR states causes errors.
    /// On iOS, Stop() internally tries to seek which can fail if the player isn't ready.
    /// </summary>
    private Task SafeStopMediaElementAsync(bool clearSource = true)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (mediaElement == null)
            {
                return;
            }

            // Only call Stop() if MediaElement is in a valid state (Playing, Paused, or Buffering)
            // On Android, calling Stop() when in IDLE or ERROR states causes errors
            var currentState = mediaElement.CurrentState;
            if (currentState is MediaElementState.Playing or
                MediaElementState.Paused or
                MediaElementState.Buffering)
            {
                try
                {
                    mediaElement.Stop();
                }
                catch (InvalidOperationException ex)
                {
                    // On iOS, Stop() internally tries to seek to zero, which can fail if the player isn't ready
                    // Log and continue - the player will be in a stopped state anyway
                    logger.Debug(ex, "Stop() failed because player isn't ready to seek, but player should be stopped");
                }
                catch (Exception ex)
                {
                    // Catch any other exceptions
                    logger.Warning(ex, "Exception occurred while stopping MediaElement in SafeStopMediaElementAsync");
                }
            }

            // Clear source if requested, or if in an invalid state (like Failed)
            if (clearSource || (currentState != MediaElementState.Stopped && currentState != MediaElementState.None))
            {
                mediaElement.Source = null;
            }
        });
    }


    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unsubscribe from current MediaElement if it exists
        if (mediaElement != null)
        {
            UnsubscribeFromMediaElement(mediaElement);
        }

        // All injected services (_mediaElementService, _displayMetadataService, _dispatcher, 
        // _androidPlayerNotificationService) are singletons, so don't dispose them
    }

    // MediaElement is now a singleton for all platforms - it is never destroyed during app lifetime
    // No DestroyMediaElementMessage handling needed

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
            mediaElement.SeekCompleted -= OnSeekCompleted;
            logger.Debug("Unsubscribed from MediaElement events");
        }
        catch (Exception ex)
        {
            // MediaElement may have been disposed, ignore
            logger.Debug(ex, "Error unsubscribing from MediaElement events (may have been disposed)");
        }
    }
}
