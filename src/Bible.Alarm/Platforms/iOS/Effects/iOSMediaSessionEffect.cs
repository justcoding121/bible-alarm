#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Services.Media;
using Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;
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

    private readonly IiOSRemoteCommandCenterManager remoteCommandManager;
    private readonly IiOSNowPlayingInfoManager nowPlayingManager;
    private readonly IState<PlaybackState> playbackState;

    // Track the last status to send correct toggle command
    private PlayStatus lastKnownStatus = PlayStatus.Stopped;

    // Metadata dedup fields to prevent redundant Now Playing updates that cause visual jitter
    private string? lastMetadataTitle;
    private string? lastMetadataArtist;
    private string? lastMetadataAlbum;
    private string? lastMetadataArtworkUrl;

    public iOSMediaSessionEffect(
        IiOSRemoteCommandCenterManager remoteCommandManager,
        IiOSNowPlayingInfoManager nowPlayingManager,
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
    /// Following iOS standard practice, we always keep the Now Playing info populated
    /// so users can control playback from the lock screen.
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

            // Update Now Playing playback status (rate = 0 for stopped/paused, 1 for playing)
            nowPlayingManager.UpdatePlaybackStatus(action.Status);

            // Update remote command availability based on current state
            var hasActiveSchedule = currentState.CurrentScheduleId.HasValue;
            var isPlaying = action.Status == PlayStatus.Playing;

            if (hasActiveSchedule)
            {
                // Active schedule - update command availability based on navigation state
                remoteCommandManager.UpdateCommandAvailability(
                    currentState.CanPlayNext,
                    currentState.CanPlayPrevious,
                    isPlaying);
            }
            else if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
            {
                // No active schedule and stopped - show Play (hide Pause) so CarPlay/lock screen display correctly
                remoteCommandManager.UpdateCommandAvailability(canPlayNext: true, canPlayPrevious: true, isPlaying: false);
                // SetDefaultScheduleMetadataAction will be dispatched and will set up the metadata
                // Commands should remain registered so play button works from lock screen
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
            if (!HasValidMetadata(action))
            {
                return Task.CompletedTask;
            }

            if (IsMetadataUnchanged(action))
            {
                logger.Debug("[iOS MediaSession] Skipping redundant metadata update for {Title}", action.Title);
                return Task.CompletedTask;
            }

            var currentState = playbackState.Value;

            logger.Information(
                "[iOS MediaSession] Metadata changed: Title={Title}, Artist={Artist}, Album={Album}",
                action.Title, action.Artist, action.Album);

            remoteCommandManager.RegisterCommands();

            nowPlayingManager.UpdateMetadata(
                action.Title,
                action.Artist,
                action.Album,
                currentState.Duration,
                action.ArtworkUrl);

            lastMetadataTitle = action.Title;
            lastMetadataArtist = action.Artist;
            lastMetadataAlbum = action.Album;
            lastMetadataArtworkUrl = action.ArtworkUrl;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling metadata change");
        }

        return Task.CompletedTask;
    }

    private bool IsMetadataUnchanged(PlaybackMetadataChangedAction action)
    {
        return action.Title == lastMetadataTitle
            && action.Artist == lastMetadataArtist
            && action.Album == lastMetadataAlbum
            && action.ArtworkUrl == lastMetadataArtworkUrl;
    }

    private void ResetMetadataDedup()
    {
        lastMetadataTitle = null;
        lastMetadataArtist = null;
        lastMetadataAlbum = null;
        lastMetadataArtworkUrl = null;
    }

    /// <summary>
    /// Handles duration changes (when track duration becomes known).
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackDurationChanged(PlaybackDurationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
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
    /// Handles setting default schedule metadata for lock screen/CarPlay Now Playing display.
    /// This follows iOS standard practice where audio apps always populate the Now Playing info
    /// with relevant content, allowing users to start playback directly from lock screen controls.
    /// </summary>
    [EffectMethod]
    public Task HandleSetDefaultScheduleMetadata(SetDefaultScheduleMetadataAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Only update if playback is not active
            if (playbackState.Value.IsPreparingOrPlaying)
            {
                return Task.CompletedTask;
            }

            // Always set default metadata - this is standard iOS practice for audio apps
            // The Now Playing controls are always present on lock screen, so we should
            // populate them with meaningful content that allows users to start playback
            logger.Information(
                "[iOS MediaSession] Setting default schedule metadata for lock screen: Title={Title}, Artist={Artist}, ScheduleId={ScheduleId}",
                action.Title, action.Artist, action.ScheduleId);

            nowPlayingManager.SetDefaultMetadata(
                action.Title,
                action.Artist,
                action.Album,
                action.ArtworkUrl);

            // Persist for CarPlay/Android Auto 5-minute rotation so next rotation shows the next schedule
            Bible.Alarm.Common.Helpers.AndroidAutoRotationHelper.SetLastRotationScheduleId(action.ScheduleId);

            // Ensure Play is shown (Pause hidden) when displaying default schedule after stop
            remoteCommandManager.UpdateCommandAvailability(canPlayNext: true, canPlayPrevious: true, isPlaying: false);

            // Register commands so play button works from lock screen
            // This allows users to start the default schedule from lock screen controls
            remoteCommandManager.RegisterCommands();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS MediaSession] Error handling default schedule metadata");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles playback stopped action - updates Now Playing to show stopped state.
    /// Following iOS standard practice, we keep the Now Playing info populated with default
    /// schedule metadata so users can restart playback from lock screen controls.
    /// The SetCarPlayScreenAction (dispatched by PlaybackStopHandler) will set the default metadata.
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackStopped(PlaybackStoppedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            logger.Information("[iOS MediaSession] Playback stopped - updating playback status and command availability");

            ResetMetadataDedup();

            nowPlayingManager.UpdatePlaybackStatus(PlayStatus.Stopped);

            // Update command availability so CarPlay/lock screen show Play (not Pause) when stopped
            remoteCommandManager.UpdateCommandAvailability(canPlayNext: true, canPlayPrevious: true, isPlaying: false);

            // Keep commands registered so play button works from lock screen
            // The play button will start the default schedule (handled by PlaybackService)
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
            var duration = message.Duration ?? currentState.Duration;
            nowPlayingManager.UpdatePlaybackPosition(
                message.CurrentPosition.Value,
                duration,
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
