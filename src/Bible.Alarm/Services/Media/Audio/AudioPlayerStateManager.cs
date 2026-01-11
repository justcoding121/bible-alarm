#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Primitives;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Audio;

/// <summary>
/// Handles state management for AudioPlayer.
/// Separated from AudioPlayer for better modularity.
/// </summary>
public class AudioPlayerStateManager
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private bool isResetting;
    private bool isSeeking;
    private PlayStatus statusBeforeSeek = PlayStatus.Stopped;
    private bool isTransitioningFromLoadingToPlaying;
    private DateTime? lastLoadingToPlayingTransitionTime;

    public PlayStatus Status { get; set; } = PlayStatus.Stopped;
    public bool IsResetting
    {
        get => isResetting;
        set => isResetting = value;
    }

    public bool IsSeeking => isSeeking;

    public AudioPlayerStateManager(ILogger logger, IDispatcher dispatcher)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
    }

    public bool ShouldIgnoreStateChange(MediaElementState newState, MediaElement? mediaElement)
    {
        // Don't update status if we're in the middle of resetting
        if (isResetting && newState != MediaElementState.Stopped)
        {
            logger.Debug("Ignoring state change to {NewState} during reset", newState);
            return true;
        }

        // Ignore Stopped state changes when we're in Loading state
        if (newState == MediaElementState.Stopped && Status == PlayStatus.Loading)
        {
            logger.Debug("Ignoring Stopped state change during Loading (preparing new track)");
            return true;
        }

        // Ignore intermediate Paused states when transitioning from Loading/Buffering to Playing
        if (newState == MediaElementState.Paused)
        {
            if (Status == PlayStatus.Loading)
            {
                isTransitioningFromLoadingToPlaying = true;
                lastLoadingToPlayingTransitionTime = DateTime.UtcNow;
                logger.Debug("Ignoring intermediate Paused state during Loading->Playing transition");
                return true;
            }
            else if (isTransitioningFromLoadingToPlaying)
            {
                var timeSinceTransition = lastLoadingToPlayingTransitionTime.HasValue
                    ? (DateTime.UtcNow - lastLoadingToPlayingTransitionTime.Value).TotalMilliseconds
                    : double.MaxValue;

                if (timeSinceTransition <= 200)
                {
                    logger.Debug("Ignoring Paused state during Loading->Playing transition ({Time}ms since transition start)", timeSinceTransition);
                    return true;
                }
                else
                {
                    isTransitioningFromLoadingToPlaying = false;
                    lastLoadingToPlayingTransitionTime = null;
                    logger.Debug("Paused state received after transition window ({Time}ms), treating as real pause", timeSinceTransition);
                }
            }
        }

        // Clear transition flag if we get Playing state
        if (newState == MediaElementState.Playing && isTransitioningFromLoadingToPlaying)
        {
            isTransitioningFromLoadingToPlaying = false;
            lastLoadingToPlayingTransitionTime = null;
        }

        // If Source is null, ignore state changes (except Stopped/None)
        if (mediaElement?.Source == null)
        {
            if (newState is not MediaElementState.Stopped and not MediaElementState.None)
            {
                logger.Debug("Ignoring state change to {NewState} because Source is null, forcing Status to Stopped", newState);
                Status = PlayStatus.Stopped;
                SendStatusMessage();
                return true;
            }
        }

        return false;
    }

    public void UpdateStatus(MediaElementState newState)
    {
        // During seeking, preserve the previous status
        if (isSeeking && newState == MediaElementState.Buffering)
        {
            Status = statusBeforeSeek;
            logger.Debug("Ignoring Buffering state change during seek, preserving status: {Status}", Status);
        }
        else
        {
            Status = newState switch
            {
                MediaElementState.Playing => PlayStatus.Playing,
                MediaElementState.Paused => PlayStatus.Paused,
                MediaElementState.Stopped => PlayStatus.Stopped,
                MediaElementState.Buffering => PlayStatus.Loading,
                MediaElementState.Failed => PlayStatus.Failed,
                MediaElementState.None => PlayStatus.Stopped,
                _ => PlayStatus.Stopped
            };
        }

        SendStatusMessage();
    }

    public void StartSeeking()
    {
        isSeeking = true;
        statusBeforeSeek = Status;
    }

    public void EndSeeking()
    {
        isSeeking = false;
    }

    public void Reset()
    {
        Status = PlayStatus.Stopped;
        isTransitioningFromLoadingToPlaying = false;
        lastLoadingToPlayingTransitionTime = null;
        isSeeking = false;
        SendStatusMessage();
    }

    private void SendStatusMessage()
    {
        logger.Debug(
            "[AudioPlayer] SendStatusMessage: Dispatching PlaybackStatusChangedAction - Status={Status}",
            Status);
        dispatcher.Dispatch(new PlaybackStatusChangedAction(Status));
    }
}

