#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Primitives;
using Serilog;

namespace Bible.Alarm.Services.Media.AudioPlayerHelpers;

/// <summary>
/// Manages MediaElement event handlers for AudioPlayer.
/// </summary>
public class EventHandlerManager
{
    private readonly ILogger logger;
    private readonly AudioPlayerStateManager stateManager;
    private readonly AudioPlayerMetadataHandler metadataHandler;
    private readonly AudioPlayerPositionTracker positionTracker;
    private readonly Func<TimeSpan?> getCurrentPosition;
    private readonly Func<TimeSpan> getDuration;
    private readonly Action<PlayStatus> setStatus;
    private readonly Action<EventArgs>? onMediaEnded;
    private readonly Action<EventArgs>? onMediaFailed;
    private readonly Func<TaskCompletionSource<bool>?> getMediaOpenedCompletionSource;
    private readonly Func<AudioPlayerTrack?> getCurrentTrack;

    public EventHandlerManager(
        ILogger logger,
        AudioPlayerStateManager stateManager,
        AudioPlayerMetadataHandler metadataHandler,
        AudioPlayerPositionTracker positionTracker,
        Func<TimeSpan?> getCurrentPosition,
        Func<TimeSpan> getDuration,
        Action<PlayStatus> setStatus,
        Action<EventArgs>? onMediaEnded,
        Action<EventArgs>? onMediaFailed,
        Func<TaskCompletionSource<bool>?> getMediaOpenedCompletionSource,
        Func<AudioPlayerTrack?> getCurrentTrack)
    {
        this.logger = logger;
        this.stateManager = stateManager;
        this.metadataHandler = metadataHandler;
        this.positionTracker = positionTracker;
        this.getCurrentPosition = getCurrentPosition;
        this.getDuration = getDuration;
        this.setStatus = setStatus;
        this.onMediaEnded = onMediaEnded;
        this.onMediaFailed = onMediaFailed;
        this.getMediaOpenedCompletionSource = getMediaOpenedCompletionSource;
        this.getCurrentTrack = getCurrentTrack;
    }

    public void SubscribeToMediaElement(MediaElement mediaElement)
    {
        mediaElement.StateChanged += OnStateChanged;
        mediaElement.MediaEnded += OnMediaEnded;
        mediaElement.MediaFailed += OnMediaFailed;
        mediaElement.MediaOpened += OnMediaOpened;
        mediaElement.PositionChanged += OnPositionChanged;
        mediaElement.SeekCompleted += OnSeekCompleted;
    }

    public void UnsubscribeFromMediaElement(MediaElement mediaElement)
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

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        try
        {
            var currentTrack = getCurrentTrack();
            if (currentTrack == null)
            {
                return;
            }

            // Signal that media is ready
            getMediaOpenedCompletionSource()?.TrySetResult(true);

            // Update duration when track opens (track change)
            positionTracker.UpdateDuration(getDuration());

            // Handle metadata
            if (sender is MediaElement mediaElement)
            {
                await metadataHandler.HandleMediaOpenedAsync(currentTrack, mediaElement);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in OnMediaOpened handler (MediaElement may have been disposed)");
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            setStatus(PlayStatus.Ended);
            onMediaEnded?.Invoke(EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in OnMediaEnded handler (MediaElement may have been disposed)");
        }
    }

    private void OnMediaFailed(object? sender, EventArgs e)
    {
        try
        {
            setStatus(PlayStatus.Failed);
            getMediaOpenedCompletionSource()?.TrySetResult(false);

            var currentTrack = getCurrentTrack();
            var trackUri = currentTrack?.Uri ?? "Unknown";
            logger.Error("MediaElement failed to play track. URI: {TrackUri}, Source: {Source}",
                trackUri,
                (sender as MediaElement)?.Source?.ToString() ?? "null");

            onMediaFailed?.Invoke(EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in OnMediaFailed handler - invoking failure callback to ensure graceful recovery");
            try
            {
                onMediaFailed?.Invoke(EventArgs.Empty);
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, "Failure callback also threw - app may show inconsistent state");
            }
        }
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        try
        {
            if (sender is not MediaElement mediaElement)
            {
                return;
            }

            if (stateManager.ShouldIgnoreStateChange(e.NewState, mediaElement))
            {
                return;
            }

            stateManager.UpdateStatus(e.NewState);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in OnStateChanged handler (MediaElement may have been disposed)");
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        try
        {
            if (stateManager.IsSeeking)
            {
                logger.Debug("[AudioPlayer] OnPositionChanged during seek - CurrentPosition: {Position}",
                    getCurrentPosition()?.ToString() ?? "null");
            }
            positionTracker.SendPositionUpdate(getCurrentPosition(), getDuration());
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in OnPositionChanged handler (MediaElement may have been disposed)");
        }
    }

    private void OnSeekCompleted(object? sender, EventArgs e)
    {
        try
        {
            var currentPosition = getCurrentPosition();
            logger.Debug("[AudioPlayer] OnSeekCompleted event fired - CurrentPosition: {Position}, Resetting _isSeeking = false",
                currentPosition?.ToString() ?? "null");
            stateManager.EndSeeking();
            logger.Debug("[AudioPlayer] Seek completed, resuming normal position and status updates");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in OnSeekCompleted handler (MediaElement may have been disposed)");
        }
    }
}

