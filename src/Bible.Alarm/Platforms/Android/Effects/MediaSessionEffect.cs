#nullable enable
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using FluxorDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.Android.Effects;

/// <summary>
/// Fluxor effect that syncs playback state and metadata with MediaSessionCompat for Android Auto.
/// This ensures that Android Auto receives the correct playback state and can route audio properly.
/// Audio focus management is handled by MediaSessionManager.
/// </summary>
public class MediaSessionEffect(
    IMediaSessionManager mediaSessionManager,
    IState<PlaybackState> playbackState,
    IAndroidArtworkService artworkService) : IRecipient<PlaybackPositionChangedMessage>
{
    private static readonly ILogger logger = Log.ForContext<MediaSessionEffect>();

    public void RegisterMessageHandlers() => WeakReferenceMessenger.Default.Register(this);

    [EffectMethod]
    public async Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var session = GetValidatedSession("cannot update playback state");
            if (session == null)
            {
                return;
            }

            var currentState = playbackState.Value;
            var isAutoAdvancing = currentState.IsAutoAdvancing;
            var previousStatus = currentState.Status;

            logger.Information(
                "[AndroidAuto] PlaybackStatusChanged: Status={NewStatus}, PreviousStatus={PreviousStatus}, IsAutoAdvancing={IsAutoAdvancing}, ScheduleId={ScheduleId}, CanPlayNext={CanPlayNext}",
                action.Status,
                previousStatus,
                isAutoAdvancing,
                currentState.CurrentScheduleId,
                currentState.CanPlayNext);

            // When status is Loading (during track preparation and media buffering),
            // Progress behavior: Always use Buffering state (shows progress animation)
            // Button behavior: Show pause button when auto-advancing (matches Alarm Modal behavior)
            // But don't show prev/next buttons during Loading - only show them when playback actually starts
            if (action.Status == PlayStatus.Loading)
            {
                // Progress: Always Buffering state (shows progress animation)
                // Button: Show pause when auto-advancing (both initial play and track transitions)
                if (isAutoAdvancing)
                {
                    // Buffering state (for progress animation) + Pause action (for button)
                    // This matches Alarm Modal: pause button visible during Loading when auto-advancing
                    logger.Information(
                        "[AndroidAuto] Loading status with auto-advancing: Setting to Buffering state with Pause action (pause button visible, buffering progress, no prev/next buttons, matches Alarm Modal)");

                    // Use Buffering state (not Playing) so progress bar shows buffering animation
                    // BuildPlaybackActions includes both Play and Pause, Android Auto shows Pause for Buffering state
                    mediaSessionManager.UpdatePlaybackState(
                        PlaybackStateCompat.StateBuffering,
                        position: 0,
                        canPlayNext: true,
                        canPlayPrevious: true);
                }
                else
                {
                    // Normal buffering - Buffering state + Play action (preserves existing actions)
                    logger.Information(
                        "[AndroidAuto] Loading status without auto-advancing: Setting to Buffering state (preserving metadata, no prev/next buttons)");
                    mediaSessionManager.SetBufferingStateOnly();
                }
            }
            else if (action.Status == PlayStatus.Stopped && isAutoAdvancing)
            {
                // During auto-advance, if status is Stopped, preserve playing state to show pause button
                var canPlayNext = true;
                var canPlayPrevious = true;
                logger.Information(
                    "[AndroidAuto] Stopped status with auto-advancing: Setting to Playing state (pause button visible, no prev/next buttons during transition)");
                mediaSessionManager.SetPlaybackStatus(PlayStatus.Playing, canPlayNext, canPlayPrevious);
            }
            else
            {
                // Next/Previous are always enabled.
                var canPlayNext = true;
                var canPlayPrevious = true;
                logger.Information(
                    "[AndroidAuto] Setting playback status to {Status} - CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                    action.Status,
                    canPlayNext,
                    canPlayPrevious);
                mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext, canPlayPrevious);
            }

            // Track playback state for foreground service coordination
            if (action.Status == PlayStatus.Playing)
            {
                // MediaElement started playing - request foreground service ownership
                await ForegroundServiceCoordinator.OnPlaybackStarted();
                SaveCurrentMetadataToPreferencesIfAvailable();
            }
            else if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
            {
                // MediaElement stopped playing - release foreground service ownership
                ForegroundServiceCoordinator.OnPlaybackStopped();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat playback state");
        }
    }

    private void SaveCurrentMetadataToPreferencesIfAvailable()
    {
        var currentState = playbackState.Value;
        LastPlayedMetadataHelper.SaveLastPlayedMetadata(
            currentState.Title,
            currentState.Artist,
            currentState.Album,
            currentState.ArtworkUrl,
            currentState.CurrentScheduleId);
    }

    [EffectMethod]
    public Task HandlePlaybackMetadataChanged(PlaybackMetadataChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var session = GetValidatedSession("cannot update metadata");
            if (session == null)
            {
                return Task.CompletedTask;
            }

            if (HasValidMetadata(action))
            {
                var metadata = BuildMetadata(action);
                if (metadata != null)
                {
                    session.SetMetadata(metadata);
                }

                // Save metadata to Preferences when playback is active (Playing or Paused)
                // This ensures we save the last played item for Android Auto screen restoration
                var currentState = playbackState.Value;
                if (currentState.Status == PlayStatus.Playing || currentState.Status == PlayStatus.Paused)
                {
                    LastPlayedMetadataHelper.SaveLastPlayedMetadata(
                        action.Title,
                        action.Artist,
                        action.Album,
                        action.ArtworkUrl,
                        currentState.CurrentScheduleId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat metadata");
        }

        return Task.CompletedTask;
    }

    [EffectMethod]
    public async Task HandleSetDefaultScheduleMetadata(SetDefaultScheduleMetadataAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Only update MediaSession if playback is not active
            // When playback is active, MediaSessionEffect.HandlePlaybackMetadataChanged handles updates
            if (playbackState.Value.IsPreparingOrPlaying)
            {
                logger.Debug("HandleSetDefaultScheduleMetadata: Playback is active, skipping default schedule metadata update");
                return;
            }

            var session = GetValidatedSession("cannot update default schedule metadata");
            if (session == null)
            {
                return;
            }

            logger.Debug("HandleSetDefaultScheduleMetadata: Updating MediaSession with default schedule - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}",
                action.ScheduleId, action.Title, action.Artist);

            // Update metadata using MediaSessionManager
            mediaSessionManager.UpdateMetadata(
                action.Title,
                action.Artist,
                action.Album,
                action.ScheduleId,
                action.ArtworkUrl);

            // Set to stopped state (idle, ready to play)
            mediaSessionManager.UpdatePlaybackStateForStop();

            // Only start Android Auto foreground service if Android Auto is actually connected
            if (ForegroundServiceCoordinator.IsAndroidAutoConnected)
            {
                // Add a delay to ensure MediaElement's notification and foreground service are fully removed
                // This ensures proper synchronization - MediaElement's notification is removed before
                // Android Auto foreground service starts. Increased delay to ensure smooth transition.
                await Task.Delay(300);

                // Double-check that MediaElement is not active before starting Android Auto foreground
                // This prevents race conditions where MediaElement might have started again
                if (!ForegroundServiceCoordinator.IsAndroidAutoConnected)
                {
                    logger.Debug("Android Auto disconnected during delay - skipping foreground service start");
                    return;
                }

                // Request Android Auto foreground service to keep connection alive
                // Uses the stored service instance from ForegroundServiceCoordinator (no DI needed)
                logger.Information("Requesting Android Auto foreground service after MediaElement disposal and metadata update");
                ForegroundServiceCoordinator.RequestForAndroidAuto(session);
            }
            else
            {
                logger.Debug("Skipping Android Auto foreground service - Android Auto is not connected");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat with default schedule metadata");
        }
    }

    private MediaSessionCompat? GetValidatedSession(string warningMessage)
    {
        var session = mediaSessionManager.GetOrCreate();
        if (session == null)
        {
            logger.Warning($"MediaSessionCompat is null, {warningMessage}");
        }
        return session;
    }

    private bool HasValidMetadata(PlaybackMetadataChangedAction action)
    {
        return !string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist);
    }

    private MediaMetadataCompat? BuildMetadata(PlaybackMetadataChangedAction action)
    {
        var scheduleId = playbackState.Value?.CurrentScheduleId;
        var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderWithMediaId(
            action.Title,
            action.Artist,
            action.Album,
            scheduleId);

        SetArtwork(metadataBuilder, action);

        return metadataBuilder.Build();
    }

    private void SetArtwork(MediaMetadataCompat.Builder metadataBuilder, PlaybackMetadataChangedAction action)
    {
        if (!string.IsNullOrEmpty(action.ArtworkUrl))
        {
            try
            {
                var artworkBitmap = artworkService?.LoadArtworkBitmap(action.ArtworkUrl);
                if (artworkBitmap != null)
                {
                    metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artworkBitmap);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", action.ArtworkUrl);
            }
        }
    }

    [EffectMethod]
    public Task HandlePlaybackDurationChanged(PlaybackDurationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var session = GetValidatedSession("cannot update duration");
            if (session == null)
            {
                return Task.CompletedTask;
            }

            UpdateDurationInMetadata(session, action.Duration);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat duration");
        }

        return Task.CompletedTask;
    }

    private void UpdateDurationInMetadata(MediaSessionCompat session, TimeSpan duration)
    {
        var durationMs = (long)duration.TotalMilliseconds;
        if (durationMs > 0)
        {
            var currentMetadata = session.Controller?.Metadata;
            if (currentMetadata != null)
            {
                var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderFromExisting(currentMetadata);
                metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, durationMs);
                session.SetMetadata(metadataBuilder.Build());
            }
        }
    }

    [EffectMethod]
    public Task HandlePlaybackNavigationChanged(PlaybackNavigationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var session = GetValidatedSession("cannot update navigation state");
            if (session == null)
            {
                return Task.CompletedTask;
            }

            var currentState = playbackState.Value;
            var state = MapPlayStatusToPlaybackState(currentState.Status);
            var position = GetCurrentPlaybackPosition(session);
            var isAutoAdvancing = currentState.IsAutoAdvancing;

            var canPlayNext = true;
            var canPlayPrevious = true;

            logger.Information(
                "[AndroidAuto] PlaybackNavigationChanged: Status={Status}, IsAutoAdvancing={IsAutoAdvancing}, CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}, ScheduleId={ScheduleId}, Position={Position}ms",
                currentState.Status,
                isAutoAdvancing,
                canPlayNext,
                canPlayPrevious,
                currentState.CurrentScheduleId,
                position);

            mediaSessionManager.UpdatePlaybackState(state, position, canPlayNext, canPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat navigation state");
        }

        return Task.CompletedTask;
    }

    private int MapPlayStatusToPlaybackState(PlayStatus status)
    {
        return status switch
        {
            PlayStatus.Playing => PlaybackStateCompat.StatePlaying,
            PlayStatus.Paused => PlaybackStateCompat.StatePaused,
            PlayStatus.Loading => PlaybackStateCompat.StateBuffering,
            PlayStatus.Stopped => PlaybackStateCompat.StateStopped,
            PlayStatus.Ended => PlaybackStateCompat.StateStopped,
            PlayStatus.Failed => PlaybackStateCompat.StateError,
            _ => PlaybackStateCompat.StateNone
        };
    }

    private long GetCurrentPlaybackPosition(MediaSessionCompat session)
    {
        var playbackStateCompat = session.Controller?.PlaybackState;
        return playbackStateCompat?.Position ?? 0;
    }

    /// <summary>
    /// Handles playback position updates to keep Android Auto progress bar synchronized.
    /// Uses duration from message when available so both position and total time are updated together,
    /// avoiding progress bar stuck at 0 when Fluxor state lags behind.
    /// </summary>
    public void Receive(PlaybackPositionChangedMessage message)
    {
        try
        {
            if (message.CurrentPosition == null)
            {
                logger.Debug("[AndroidAuto] PlaybackPositionChangedMessage: CurrentPosition is null, skipping");
                return;
            }

            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                logger.Debug("[AndroidAuto] PlaybackPositionChangedMessage: MediaSession is null, skipping");
                return;
            }

            var duration = message.Duration ?? playbackState.Value.Duration;
            UpdatePlaybackPosition(session, message.CurrentPosition.Value, duration);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat playback position");
        }
    }

    private void UpdatePlaybackPosition(MediaSessionCompat session, TimeSpan currentPosition, TimeSpan duration)
    {
        var currentState = playbackState.Value;
        var canPlayNext = currentState.CanPlayNext;

        // Always enable previous button for Android Auto (even on first track - will restart current track)
        mediaSessionManager.UpdatePlaybackPosition(currentPosition, duration, canPlayNext, canPlayPrevious: true);
    }
}
