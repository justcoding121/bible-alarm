#nullable enable
using System;
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

    private bool messageHandlersRegistered;

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
        if (messageHandlersRegistered)
        {
            logger.Debug("[iOS MediaSession] RegisterMessageHandlers called again — already registered, skipping");
            return;
        }

        messageHandlersRegistered = true;
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
            var currentState = playbackState.Value;

            logger.Information(
                "[iOS MediaSession] PlaybackStatusChanged: Status={NewStatus}, ScheduleId={ScheduleId}, CanPlayNext={CanPlayNext}, IsAutoAdvancing={IsAutoAdvancing}",
                action.Status,
                currentState.CurrentScheduleId,
                currentState.CanPlayNext,
                currentState.IsAutoAdvancing);

            if (action.Status == PlayStatus.Loading)
            {
                // Fluxor effects for the same action type can run concurrently.
                // If Loading and Playing are dispatched close together, the Playing
                // effect may update the status before this Loading effect runs.
                // Re-check Fluxor to avoid overwriting Playing with a stale Loading rate.
                if (playbackState.Value.Status != PlayStatus.Loading)
                {
                    logger.Debug(
                        "[iOS MediaSession] Skipping Loading status — Fluxor already advanced to {Status}",
                        playbackState.Value.Status);
                    return Task.CompletedTask;
                }
            }
            else if (action.Status == PlayStatus.Stopped)
            {
                // Re-read isAutoAdvancing from current Fluxor state (not the snapshot).
                // During auto-advance, Stopped fires before SetAutoAdvancingAction(true),
                // but by the time this async effect executes, the flag may be updated.
                var currentAutoAdvancing = playbackState.Value.IsAutoAdvancing;
                if (currentAutoAdvancing)
                {
                    logger.Information(
                        "[iOS MediaSession] Stopped with auto-advancing: keeping Playing rate to prevent CarPlay pause flash");
                    nowPlayingManager.UpdatePlaybackStatus(PlayStatus.Playing);
                    return Task.CompletedTask;
                }
            }
            else if (action.Status == PlayStatus.Paused)
            {
                var currentState2 = playbackState.Value;
                if (currentState2.IsAutoAdvancing || currentState2.IsTransitioningTrack)
                {
                    logger.Information(
                        "[iOS MediaSession] Paused during track transition: keeping Playing rate to prevent CarPlay play button flash (IsAutoAdvancing={IsAutoAdvancing}, IsTransitioningTrack={IsTransitioningTrack})",
                        currentState2.IsAutoAdvancing,
                        currentState2.IsTransitioningTrack);
                    nowPlayingManager.UpdatePlaybackStatus(PlayStatus.Playing);
                    return Task.CompletedTask;
                }
            }

            nowPlayingManager.UpdatePlaybackStatus(action.Status);

            var hasActiveSchedule = currentState.CurrentScheduleId.HasValue;
            var isPlaying = action.Status == PlayStatus.Playing;

            if (hasActiveSchedule)
            {
                remoteCommandManager.UpdateCommandAvailability(
                    currentState.CanPlayNext,
                    currentState.CanPlayPrevious,
                    isPlaying);
            }
            else if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
            {
                remoteCommandManager.UpdateCommandAvailability(canPlayNext: true, canPlayPrevious: true, isPlaying: false);
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

            // During track transitions the reducer resets duration to 0 before the
            // new track's duration is known. Preserve the previous duration so the
            // time display on CarPlay/lock screen doesn't disappear momentarily.
            var duration = currentState.Duration;
            if (duration <= TimeSpan.Zero)
            {
                duration = nowPlayingManager.GetCurrentDuration();
            }

            nowPlayingManager.UpdateMetadata(
                action.Title,
                action.Artist,
                action.Album,
                duration,
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
        return string.Equals(action.Title, lastMetadataTitle, StringComparison.Ordinal)
            && string.Equals(action.Artist, lastMetadataArtist, StringComparison.Ordinal)
            && string.Equals(action.Album, lastMetadataAlbum, StringComparison.Ordinal)
            && string.Equals(action.ArtworkUrl, lastMetadataArtworkUrl, StringComparison.Ordinal);
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

            // Skip navigation updates during transitional states.
            // HandlePlaybackStatusChanged already sets command availability for these;
            // re-applying here causes unnecessary toggling of play/pause commands.
            if (currentState.Status is PlayStatus.Loading or PlayStatus.Stopped or PlayStatus.Ended)
            {
                return Task.CompletedTask;
            }

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

            // Skip stale position updates after playback has stopped/ended.
            var status = playbackState.Value.Status;
            if (status is PlayStatus.Stopped or PlayStatus.Ended)
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

    private static bool HasValidMetadata(PlaybackMetadataChangedAction action)
    {
        return !string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist);
    }
}
