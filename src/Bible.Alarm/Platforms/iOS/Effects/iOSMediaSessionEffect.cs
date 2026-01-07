#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Services.Media;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using FluxorDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.iOS.Effects;

/// <summary>
/// Fluxor effect that syncs playback state and metadata with iOS Now Playing and Remote Command Center.
/// This ensures that Lock Screen, Control Center, CarPlay, AirPods, and other system media interfaces
/// receive the correct playback state and can control audio properly.
/// 
/// Mirrors Android's MediaSessionEffect but uses iOS-specific APIs:
/// - MPNowPlayingInfoCenter (equivalent to MediaMetadataCompat)
/// - MPRemoteCommandCenter (equivalent to MediaSessionCompat.Callback)
/// </summary>
public class iOSMediaSessionEffect : IRecipient<PlaybackPositionChangedMessage>
{
    private static readonly ILogger logger = Log.ForContext<iOSMediaSessionEffect>();

    private readonly iOSRemoteCommandCenterManager remoteCommandManager;
    private readonly iOSNowPlayingInfoManager nowPlayingManager;
    private readonly IState<PlaybackState> playbackState;

    // Track the last status to send correct toggle command
    private PlayStatus lastKnownStatus = PlayStatus.Stopped;

    public iOSMediaSessionEffect(
        iOSRemoteCommandCenterManager remoteCommandManager,
        iOSNowPlayingInfoManager nowPlayingManager,
        IState<PlaybackState> playbackState)
    {
        this.remoteCommandManager = remoteCommandManager;
        this.nowPlayingManager = nowPlayingManager;
        this.playbackState = playbackState;
    }

    /// <summary>
    /// Registers message handlers for position updates.
    /// Call this during app initialization.
    /// </summary>
    public void RegisterMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register(this);
        // Register remote commands when the effect is initialized
        remoteCommandManager.RegisterCommands();
        logger.Information("[iOS MediaSession] Effect initialized and message handlers registered");
    }

    /// <summary>
    /// Handles playback status changes (playing, paused, stopped, etc.).
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            lastKnownStatus = action.Status;
            var currentState = playbackState.Value;

            logger.Information(
                "[iOS MediaSession] PlaybackStatusChanged: Status={NewStatus}, ScheduleId={ScheduleId}, CanPlayNext={CanPlayNext}",
                action.Status,
                currentState.CurrentScheduleId,
                currentState.CanPlayNext);

            // Update Now Playing playback status
            nowPlayingManager.UpdatePlaybackStatus(action.Status);

            // Update remote command availability
            var isPlaying = action.Status == PlayStatus.Playing;
            remoteCommandManager.UpdateCommandAvailability(
                currentState.CanPlayNext,
                currentState.CanPlayPrevious,
                isPlaying);

            // If playback stopped/ended, clear Now Playing after a delay to allow for track transitions
            if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
            {
                // Don't clear immediately - allow for auto-advance between tracks
                // The next track will update the metadata
                logger.Debug("[iOS MediaSession] Playback stopped/ended - metadata will be updated by next action");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling playback status change");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles metadata changes (title, artist, album, artwork).
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackMetadataChanged(PlaybackMetadataChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;

            if (HasValidMetadata(action))
            {
                logger.Information(
                    "[iOS MediaSession] Metadata changed: Title={Title}, Artist={Artist}, Album={Album}",
                    action.Title, action.Artist, action.Album);

                nowPlayingManager.UpdateMetadata(
                    action.Title,
                    action.Artist,
                    action.Album,
                    currentState.Duration,
                    action.ArtworkUrl);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling metadata change");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles duration changes (when track duration becomes known).
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackDurationChanged(PlaybackDurationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            logger.Debug("[iOS MediaSession] Duration changed: {Duration}", action.Duration);
            nowPlayingManager.UpdateDuration(action.Duration);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling duration change");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles navigation state changes (can play next/previous).
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackNavigationChanged(PlaybackNavigationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;
            var isPlaying = currentState.Status == PlayStatus.Playing;

            logger.Debug(
                "[iOS MediaSession] Navigation changed: CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                action.CanPlayNext, action.CanPlayPrevious);

            remoteCommandManager.UpdateCommandAvailability(
                action.CanPlayNext,
                action.CanPlayPrevious,
                isPlaying);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling navigation change");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles setting default schedule metadata (for CarPlay/Now Playing when not actively playing).
    /// </summary>
    [EffectMethod]
    public Task HandleSetDefaultScheduleMetadata(SetDefaultScheduleMetadataAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Only update if playback is not active
            if (playbackState.Value.IsPreparingOrPlaying)
            {
                logger.Debug("[iOS MediaSession] Playback is active, skipping default schedule metadata update");
                return Task.CompletedTask;
            }

            logger.Debug(
                "[iOS MediaSession] Setting default schedule metadata: Title={Title}, Artist={Artist}",
                action.Title, action.Artist);

            nowPlayingManager.SetDefaultMetadata(
                action.Title,
                action.Artist,
                action.Album,
                action.ArtworkUrl);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error setting default schedule metadata");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles playback stopped action - clears Now Playing info.
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackStopped(PlaybackStoppedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            logger.Debug("[iOS MediaSession] Playback stopped - clearing Now Playing info");
            nowPlayingManager.ClearNowPlayingInfo();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling playback stopped");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles playback position updates to keep Now Playing progress synchronized.
    /// </summary>
    public void Receive(PlaybackPositionChangedMessage message)
    {
        try
        {
            if (message.CurrentPosition == null)
            {
                return;
            }

            var currentState = playbackState.Value;
            nowPlayingManager.UpdatePlaybackPosition(
                message.CurrentPosition.Value,
                currentState.Duration,
                currentState.Status);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error updating playback position");
        }
    }

    private bool HasValidMetadata(PlaybackMetadataChangedAction action)
    {
        return !string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist);
    }
}
