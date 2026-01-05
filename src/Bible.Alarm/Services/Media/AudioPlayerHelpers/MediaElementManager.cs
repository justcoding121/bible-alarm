#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Essentials;
using Serilog;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Maui.Core;

#if ANDROID
#endif
#if IOS
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.Media.AudioPlayerHelpers;

/// <summary>
/// Manages MediaElement lifecycle for AudioPlayer.
/// </summary>
public class MediaElementManager
{
    private readonly ILogger logger;
    private readonly IMediaElementService mediaElementService;
    private readonly AudioPlayerStateManager stateManager;
    private readonly AudioPlayerPositionTracker positionTracker;
    private readonly EventHandlerManager eventHandlerManager;
#if ANDROID
    private readonly IAndroidPlayerNotificationService? androidPlayerNotificationService;
#endif

    public MediaElementManager(
        ILogger logger,
        IMediaElementService mediaElementService,
        AudioPlayerStateManager stateManager,
        AudioPlayerPositionTracker positionTracker,
        EventHandlerManager eventHandlerManager
#if ANDROID
        , IAndroidPlayerNotificationService? androidPlayerNotificationService = null
#endif
        )
    {
        this.logger = logger;
        this.mediaElementService = mediaElementService;
        this.stateManager = stateManager;
        this.positionTracker = positionTracker;
        this.eventHandlerManager = eventHandlerManager;
#if ANDROID
        this.androidPlayerNotificationService = androidPlayerNotificationService;
#endif
    }

    public async Task<MediaElement> PrepareAsync(
        MediaElement? currentMediaElement,
        AudioPlayerTrack track,
        bool isFirstTrack = false,
        bool isLastTrack = false)
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
        if (currentMediaElement != null)
        {
            if (currentMediaElement != newMediaElement)
            {
                logger.Information("MediaElement instance changed - unsubscribing from old instance");
            }
            eventHandlerManager.UnsubscribeFromMediaElement(currentMediaElement);
        }

        // Update reference and attach event handlers
        // Attach event handlers to the MediaElement
        eventHandlerManager.SubscribeToMediaElement(newMediaElement);

        await SafeStopMediaElementAsync(newMediaElement, clearSource: false);

        stateManager.Status = PlayStatus.Loading;
        // Reset duration tracking so new track's duration will be detected as changed
        positionTracker.ResetDuration();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Clear previous artwork to prevent iOS from trying to load null data
            newMediaElement.MetadataArtworkUrl = null!;

#if IOS
            var processedUri = IosMediaElementHelper.ProcessUriForMediaElement(track.Uri, logger);
            IosMediaElementHelper.SetSourceAndVolume(newMediaElement, processedUri, logger);
#elif ANDROID
            // Handler is guaranteed to exist - GetMediaElementAsync() creates it for headless mode
            // CRITICAL: We MUST set a valid Source on MediaElement itself
            // even when using ExoPlayer directly for the queue.
            // Otherwise, MediaElement stays in State.None forever.
            newMediaElement.Source = MediaSource.FromUri(track.Uri);

            // Now set the real (multi-item) queue via ExoPlayer to enable Next button
            // Pass MediaElement to the service with flags indicating track position
            androidPlayerNotificationService?.SetSourceWithDummyQueue(newMediaElement, track.Uri, isFirstTrack, isLastTrack);
#else
            newMediaElement.Source = track.Uri;
#endif
        });

        return newMediaElement;
    }

    public async Task ResetAsync(MediaElement? mediaElement, Action resetState)
    {
        stateManager.IsResetting = true;

        try
        {
            // Stop and clear source
            await SafeStopMediaElementAsync(mediaElement, clearSource: true);

            // Wait a bit for the stop to take effect
            await Task.Delay(100);

            // Force stop again if still in a valid state to stop
            var actualState = await MainThread.InvokeOnMainThreadAsync(() => mediaElement?.CurrentState ?? MediaElementState.None);
            if (actualState is MediaElementState.Playing or
                MediaElementState.Paused or
                MediaElementState.Buffering)
            {
                logger.Debug("MediaElement still in {State} state after first stop, forcing stop again", actualState);
                await SafeStopMediaElementAsync(mediaElement, clearSource: true);
                await Task.Delay(100);
            }

            // Ensure status is reset to Stopped
            resetState();

#if ANDROID
            // Release MediaSession to hide the media notification
            // This is the ONLY place where we call ReleaseMediaSession - only on final stop, not during track changes
            // Add a small delay to ensure MediaElement has fully stopped before releasing
            logger.Information("Resetting playback - calling ReleaseMediaSession to remove notification");
            // Small delay to let MediaElement finish stopping
            await Task.Delay(100);
            if (mediaElement != null && androidPlayerNotificationService != null)
            {
                await androidPlayerNotificationService.ReleaseMediaSessionAsync(mediaElement);
            }
            logger.Information("ReleaseMediaSession called - notification should be removed");
#endif

            // Dispose MediaElement to free up resources (ExoPlayer, MediaSession, etc.)
            if (mediaElement != null)
            {
                logger.Information("Disposing MediaElement instance to free up resources");
                await mediaElementService.DisposeMediaElementAsync();
                logger.Information("MediaElement disposed - resources freed");
            }

            // Verify final state
            logger.Debug("Reset completed. Status: {Status}", stateManager.Status);
        }
        finally
        {
            stateManager.IsResetting = false;
        }
    }

    /// <summary>
    /// Safely stops the MediaElement by checking its state first.
    /// On Android, calling Stop() when in IDLE or ERROR states causes errors.
    /// On iOS, Stop() internally tries to seek which can fail if the player isn't ready.
    /// </summary>
    private Task SafeStopMediaElementAsync(MediaElement? mediaElement, bool clearSource = true)
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
}

