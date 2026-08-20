#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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

    public EventHandlerManager(EventHandlerManagerDeps deps, EventHandlerManagerCallbacks callbacks)
    {
        logger = deps.Logger;
        stateManager = deps.StateManager;
        metadataHandler = deps.MetadataHandler;
        positionTracker = deps.PositionTracker;
        getCurrentPosition = callbacks.GetCurrentPosition;
        getDuration = callbacks.GetDuration;
        setStatus = callbacks.SetStatus;
        onMediaEnded = callbacks.OnMediaEnded;
        onMediaFailed = callbacks.OnMediaFailed;
        getMediaOpenedCompletionSource = callbacks.GetMediaOpenedCompletionSource;
        getCurrentTrack = callbacks.GetCurrentTrack;
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
            logger.Debug(AppConstants.Logging.MediaElementHandlerDiagnosticsLog.UnsubscribedFromMediaElementEvents);
        }
        catch (Exception ex)
        {
            // MediaElement may have been disposed, ignore
            logger.Debug(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.ErrorUnsubscribingFromMediaElementEventsMayHaveBeenDisposed);
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

            positionTracker.UpdateDuration(getDuration());

            if (sender is MediaElement mediaElement)
            {
                await metadataHandler.HandleMediaOpenedAsync(currentTrack, mediaElement);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnMediaOpenedMayBeDisposed);
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
            logger.Warning(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnMediaEndedMayBeDisposed);
        }
    }

    private void OnMediaFailed(object? sender, EventArgs e)
    {
        try
        {
            setStatus(PlayStatus.Failed);
            getMediaOpenedCompletionSource()?.TrySetResult(false);

            var currentTrack = getCurrentTrack();
            var trackUri = currentTrack?.Uri ?? MediaTrackTitleHelper.UnknownTitle;
            logger.Error(AppConstants.Logging.MediaElementHandlerDiagnosticsLog.MediaElementFailedToPlayTrackUriSource,
                trackUri,
                (sender as MediaElement)?.Source?.ToString() ?? "null");

            onMediaFailed?.Invoke(EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnMediaFailedInvokingFailureCallback);
            try
            {
                onMediaFailed?.Invoke(EventArgs.Empty);
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnMediaFailedFailureCallbackThrew);
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
            logger.Warning(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnStateChangedMayBeDisposed);
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        try
        {
            if (stateManager.IsSeeking)
            {
                logger.Debug(AppConstants.Logging.MediaElementHandlerDiagnosticsLog.AudioPlayerOnPositionChangedDuringSeekCurrentPosition,
                    getCurrentPosition()?.ToString() ?? "null");
            }
            positionTracker.SendPositionUpdate(getCurrentPosition(), getDuration());
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnPositionChangedMayBeDisposed);
        }
    }

    private void OnSeekCompleted(object? sender, EventArgs e)
    {
        try
        {
            var currentPosition = getCurrentPosition();
            logger.Debug(AppConstants.Logging.MediaElementHandlerDiagnosticsLog.AudioPlayerOnSeekCompletedEventFiredResettingSeeking,
                currentPosition?.ToString() ?? "null");

            if (sender is MediaElement mediaElement)
            {
                stateManager.EndSeekingAndReevaluateState(mediaElement.CurrentState);
            }
            else
            {
                stateManager.EndSeeking();
            }

            logger.Debug(AppConstants.Logging.MediaElementHandlerDiagnosticsLog.AudioPlayerSeekCompletedResumingNormalPositionAndStatusUpdates);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MediaElementHandlerDiagnosticsLog.OnSeekCompletedMayBeDisposed);
        }
    }
}

