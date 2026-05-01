#nullable enable
using System;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Constants;
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

    private static volatile bool isRestartingPlayback;

    // Dedup tracking: skip redundant metadata updates that cause visual flickering
    // on the car screen (same track metadata dispatched multiple times per track).
    private string? lastMetadataTitle;
    private string? lastMetadataArtist;
    private string? lastMetadataAlbum;
    private string? lastMetadataArtworkUrl;

    /// <summary>
    /// Suppresses intermediate Stopped/Ended MediaSession state updates during
    /// a stop-and-restart transition (e.g. Android Auto OnPlay tap) to prevent
    /// rapid play/pause button flashing on the car screen.
    /// </summary>
    internal static void SetRestartingPlayback(bool value) => isRestartingPlayback = value;

    public void RegisterMessageHandlers() => WeakReferenceMessenger.Default.Register(this);

    [EffectMethod]
    public async Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            if (GetValidatedSession("cannot update playback state") == null)
            {
                return;
            }

            await SyncAndroidAutoPlaybackStatusAsync(action);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.AndroidMediaSessionCompatUpdateDiagnosticsLog.ErrorUpdatingPlaybackState);
        }
    }

    private async Task SyncAndroidAutoPlaybackStatusAsync(PlaybackStatusChangedAction action)
    {
        var currentState = playbackState.Value;
        var isAutoAdvancing = currentState.IsAutoAdvancing;
        var previousStatus = currentState.Status;

        if (TryShortCircuitRestartingPlayback(action))
        {
            return;
        }

        logger.Information(
            "[AndroidAuto] PlaybackStatusChanged: Status={NewStatus}, PreviousStatus={PreviousStatus}, IsAutoAdvancing={IsAutoAdvancing}, ScheduleId={ScheduleId}, CanPlayNext={CanPlayNext}",
            action.Status,
            previousStatus,
            isAutoAdvancing,
            currentState.CurrentScheduleId,
            currentState.CanPlayNext);

        if (!TryApplyPlaybackStatusBranch(action, isAutoAdvancing))
        {
            return;
        }

        await CoordinatePlaybackLifecycleSideEffectsAsync(action);
        ApplyMediaSessionActivationPolicyForMediaElementOwner();
    }

    /// <summary>Returns true when the handler should exit early (restart suppression path).</summary>
    private bool TryShortCircuitRestartingPlayback(PlaybackStatusChangedAction action)
    {
        if (!isRestartingPlayback)
        {
            return false;
        }

        if (action.Status is PlayStatus.Stopped or PlayStatus.Ended)
        {
            logger.Information(
                "[AndroidAuto] Suppressing {Status} MediaSession update during playback restart to prevent flashing",
                action.Status);
            ForegroundServiceCoordinator.OnPlaybackStopped();
            return true;
        }

        SetRestartingPlayback(false);
        logger.Information(
            "[AndroidAuto] Cleared restart suppression flag on {Status} status", action.Status);
        return false;
    }

    /// <summary>Returns false when Loading branch skips the rest (Fluxor already advanced past Loading).</summary>
    private bool TryApplyPlaybackStatusBranch(PlaybackStatusChangedAction action, bool isAutoAdvancing)
    {
        switch (action.Status)
        {
            case PlayStatus.Loading:
                return ApplyLoadingPlaybackBranch(isAutoAdvancing);
            case PlayStatus.Stopped:
                ApplyStoppedPlaybackBranch(action);
                return true;
            case PlayStatus.Paused:
                ApplyPausedPlaybackBranch(action);
                return true;
            case PlayStatus.Failed:
                ApplyFailedPlaybackBranch();
                return true;
            default:
                ApplyDefaultPlaybackBranch(action);
                return true;
        }
    }

    private bool ApplyLoadingPlaybackBranch(bool isAutoAdvancing)
    {
        if (playbackState.Value.Status != PlayStatus.Loading)
        {
            logger.Debug(
                "[AndroidAuto] Skipping Buffering state — Fluxor status already advanced past Loading to {Status}",
                playbackState.Value.Status);
            return false;
        }

        if (isAutoAdvancing)
        {
            logger.Information(
                "[AndroidAuto] Loading status with auto-advancing: Setting to Buffering state with Pause action (pause button visible, buffering progress, no prev/next buttons, matches Alarm Modal)");
            mediaSessionManager.UpdatePlaybackState(
                PlaybackStateCompat.StateBuffering,
                position: 0,
                canPlayNext: true,
                canPlayPrevious: true);
        }
        else
        {
            logger.Information(
                "[AndroidAuto] Loading status without auto-advancing: Setting to Buffering state (preserving metadata, no prev/next buttons)");
            mediaSessionManager.SetBufferingStateOnly();
        }

        return true;
    }

    private void ApplyStoppedPlaybackBranch(PlaybackStatusChangedAction action)
    {
        var currentAutoAdvancing = playbackState.Value.IsAutoAdvancing;
        const bool canPlayNext = true;
        const bool canPlayPrevious = true;
        if (currentAutoAdvancing)
        {
            logger.Information(
                "[AndroidAuto] Stopped status with auto-advancing: Setting to Playing state (pause button visible, no prev/next buttons during transition)");
            mediaSessionManager.SetPlaybackStatus(PlayStatus.Playing, canPlayNext, canPlayPrevious);
        }
        else
        {
            logger.Information(
                "[AndroidAuto] Setting playback status to {Status} - CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                action.Status,
                canPlayNext,
                canPlayPrevious);
            mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext, canPlayPrevious);
        }
    }

    private void ApplyPausedPlaybackBranch(PlaybackStatusChangedAction action)
    {
        var snapshot = playbackState.Value;
        if (snapshot.IsAutoAdvancing || snapshot.IsTransitioningTrack)
        {
            logger.Information(
                "[AndroidAuto] Paused status during track transition: keeping Playing state to prevent play button flash (IsAutoAdvancing={IsAutoAdvancing}, IsTransitioningTrack={IsTransitioningTrack})",
                snapshot.IsAutoAdvancing,
                snapshot.IsTransitioningTrack);
            mediaSessionManager.SetPlaybackStatus(PlayStatus.Playing, canPlayNext: true, canPlayPrevious: true);
        }
        else
        {
            logger.Information(
                "[AndroidAuto] Setting playback status to {Status} - CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                action.Status,
                true,
                true);
            mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext: true, canPlayPrevious: true);
        }
    }

    private void ApplyFailedPlaybackBranch()
    {
        var errorMessage = playbackState.Value.ErrorMessage;
        logger.Information(
            "[AndroidAuto] Playback failed — setting error state with message on Now Playing screen: {ErrorMessage}",
            errorMessage);
        mediaSessionManager.SetErrorState(errorMessage, canPlayNext: true, canPlayPrevious: true);
    }

    private void ApplyDefaultPlaybackBranch(PlaybackStatusChangedAction action)
    {
        const bool canPlayNext = true;
        const bool canPlayPrevious = true;
        logger.Information(
            "[AndroidAuto] Setting playback status to {Status} - CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
            action.Status,
            canPlayNext,
            canPlayPrevious);
        mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext, canPlayPrevious);
    }

    private async Task CoordinatePlaybackLifecycleSideEffectsAsync(PlaybackStatusChangedAction action)
    {
        if (action.Status == PlayStatus.Playing)
        {
            await ForegroundServiceCoordinator.OnPlaybackStarted();
            SaveCurrentMetadataToPreferencesIfAvailable();
        }
        else if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
        {
            ForegroundServiceCoordinator.OnPlaybackStopped();
            ResetMetadataDedup();
        }
    }

    private void ApplyMediaSessionActivationPolicyForMediaElementOwner()
    {
        if (!ForegroundServiceCoordinator.IsAndroidAutoConnected
            && ForegroundServiceCoordinator.CurrentOwner == ForegroundServiceCoordinator.ForegroundServiceOwner.MediaElement)
        {
            mediaSessionManager.SetActive(false);
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
                if (IsMetadataUnchanged(action))
                {
                    logger.Debug("[AndroidAuto] Skipping redundant metadata update (title/artist/album/artwork unchanged)");
                    return Task.CompletedTask;
                }

                var metadata = BuildMetadata(action, session);
                if (metadata != null)
                {
                    session.SetMetadata(metadata);
                    var durationMs = metadata.GetLong(MediaMetadataCompat.MetadataKeyDuration);
                    mediaSessionManager.SetTrackedDuration(durationMs);

                    lastMetadataTitle = action.Title;
                    lastMetadataArtist = action.Artist;
                    lastMetadataAlbum = action.Album;
                    lastMetadataArtworkUrl = action.ArtworkUrl;
                }

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
            if (playbackState.Value.IsPreparingOrPlaying || isRestartingPlayback)
            {
                logger.Debug("HandleSetDefaultScheduleMetadata: Playback is active or restarting, skipping default schedule metadata update");
                return;
            }

            // Only update MediaSession and show default notification when the car is actually connected.
            // Uses the CarConnection content provider (hosted by Google's Android Auto app) which
            // reflects real-time physical connection state. This is more reliable than:
            // - ForegroundServiceCoordinator.IsAndroidAutoConnected: can stay stale true after
            //   physical disconnect (MediaBrowserService delayed unbind / quick-reconnect).
            // - App.IsInForeground: would break the passenger scenario (user on phone while in car).
            if (!CarConnectionHelper.IsCarConnected())
            {
                // If the bind flag is still stale, OnUnbind was never called (common with
                // wireless Android Auto on some cars). Clean up the orphaned foreground
                // notification and deactivate the MediaSession to prevent Android 13+
                // from auto-generating a system notification for the stale active session.
                if (ForegroundServiceCoordinator.IsAndroidAutoConnected)
                {
                    logger.Warning(
                        "HandleSetDefaultScheduleMetadata: Car physically disconnected but MediaBrowser bind flag still true — cleaning up stale Android Auto state");
                    ForegroundServiceCoordinator.OnAndroidAutoDisconnected();
                }

                mediaSessionManager.SetActive(false);
                logger.Debug("HandleSetDefaultScheduleMetadata: Car not connected (CarConnection provider), skipping default metadata/notification");
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

            // Persist this schedule as "last shown" for 5-minute rotation so the next rotation shows the next schedule
            AndroidAutoRotationHelper.SetLastRotationScheduleId(action.ScheduleId);

            // Only start Android Auto foreground service if the car is still connected.
            // Re-check via the CarConnection content provider (not the stale binding flag)
            // to handle race conditions where the user disconnected during metadata update.
            if (ForegroundServiceCoordinator.IsAndroidAutoConnected && CarConnectionHelper.IsCarConnected())
            {
                // Add a delay to ensure MediaElement's notification and foreground service are fully removed
                // This ensures proper synchronization - MediaElement's notification is removed before
                // Android Auto foreground service starts. Increased delay to ensure smooth transition.
                await Task.Delay(300);

                // Double-check car is still connected after delay
                if (!CarConnectionHelper.IsCarConnected())
                {
                    logger.Debug("Car disconnected during delay - skipping foreground service start");
                    return;
                }

                // Request Android Auto foreground service to keep connection alive
                // Uses the stored service instance from ForegroundServiceCoordinator (no DI needed)
                logger.Information("Requesting Android Auto foreground service after MediaElement disposal and metadata update");
                ForegroundServiceCoordinator.RequestForAndroidAuto(session);
            }
            else
            {
                logger.Debug("Skipping Android Auto foreground service - car not connected");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.AndroidMediaSessionCompatUpdateDiagnosticsLog.ErrorUpdatingDefaultScheduleMetadata);
        }
    }

    private MediaSessionCompat? GetValidatedSession(string warningMessage)
    {
        var session = mediaSessionManager.GetOrCreate();
        if (session == null)
        {
            logger.Warning("MediaSessionCompat is null, {Detail}", warningMessage);
        }
        return session;
    }

    private static bool HasValidMetadata(PlaybackMetadataChangedAction action)
    {
        return !string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist);
    }

    private MediaMetadataCompat? BuildMetadata(PlaybackMetadataChangedAction action, MediaSessionCompat session)
    {
        var scheduleId = playbackState.Value?.CurrentScheduleId;
        var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderWithMediaId(
            action.Title,
            action.Artist,
            action.Album,
            scheduleId);

        SetArtwork(metadataBuilder, action, session);

        // Include duration from Fluxor state so metadata replacement doesn't wipe duration.
        // Without this, a race between PlaybackMetadataChangedAction and PlaybackDurationChangedAction
        // can cause the progress bar in Android Auto to lose the end time.
        var duration = playbackState.Value?.Duration ?? TimeSpan.Zero;
        if (duration > TimeSpan.Zero)
        {
            metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, (long)duration.TotalMilliseconds);
        }
        else
        {
            // During track transitions, the reducer resets duration to 0 before the new
            // track's duration is known. Preserve the existing duration from the MediaSession
            // to prevent the time text from disappearing and causing layout shifts on the car screen.
            // Always set duration (even 0) so the time area stays allocated on the Now Playing screen.
            var existingDuration = session.Controller?.Metadata?.GetLong(MediaMetadataCompat.MetadataKeyDuration) ?? 0;
            metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, Math.Max(existingDuration, 0));
        }

        return metadataBuilder.Build();
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

    private void SetArtwork(MediaMetadataCompat.Builder metadataBuilder, PlaybackMetadataChangedAction action, MediaSessionCompat session)
    {
        if (!string.IsNullOrEmpty(action.ArtworkUrl))
        {
            try
            {
                var artworkBitmap = artworkService?.LoadArtworkBitmap(action.ArtworkUrl);
                if (artworkBitmap != null)
                {
                    metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artworkBitmap);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.AndroidMediaArtworkLog.ErrorLoadingBitmapFromArtworkUrl, action.ArtworkUrl);
            }
        }

        // No new artwork - preserve existing artwork from the MediaSession to prevent
        // blank artwork during transitions (while async artwork extraction is pending).
        // If no existing artwork either (fresh start), the car UI shows no art (no app icon fallback).
        var existingArtwork = session.Controller?.Metadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
        if (existingArtwork != null)
        {
            metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
        }
    }

    [EffectMethod]
    public Task HandlePlaybackDurationChanged(PlaybackDurationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            mediaSessionManager.UpdateDuration(action.Duration);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat duration");
        }

        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task HandlePlaybackNavigationChanged(PlaybackNavigationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;

            // Skip navigation updates during transitional states.
            // HandlePlaybackStatusChanged already sets the correct state for these;
            // re-applying the state here from the Fluxor status causes conflicting
            // SetPlaybackState calls and rapid play/pause button flashing.
            if (currentState.Status is PlayStatus.Loading or PlayStatus.Stopped or PlayStatus.Ended)
            {
                return Task.CompletedTask;
            }

            var session = GetValidatedSession("cannot update navigation state");
            if (session == null)
            {
                return Task.CompletedTask;
            }

            var state = MapPlayStatusToPlaybackState(currentState.Status);
            var position = GetCurrentPlaybackPosition(session);

            var canPlayNext = true;
            var canPlayPrevious = true;

            logger.Information(
                "[AndroidAuto] PlaybackNavigationChanged: Status={Status}, CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}, ScheduleId={ScheduleId}, Position={Position}ms",
                currentState.Status,
                canPlayNext,
                canPlayPrevious,
                currentState.CurrentScheduleId,
                position);

            mediaSessionManager.UpdatePlaybackState(state, position, canPlayNext, canPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.AndroidMediaSessionCompatUpdateDiagnosticsLog.ErrorUpdatingNavigationState);
        }

        return Task.CompletedTask;
    }

    private static int MapPlayStatusToPlaybackState(PlayStatus status)
    {
        // Stopped/Ended use StatePaused (not StateStopped) to stay consistent with
        // SetStoppedState, which uses StatePaused to hint Android Auto that media is
        // "ready" rather than "unavailable". Inconsistent mapping causes conflicting
        // SetPlaybackState calls between handlers → rapid play/pause button flash.
        return status switch
        {
            PlayStatus.Playing => PlaybackStateCompat.StatePlaying,
            PlayStatus.Paused => PlaybackStateCompat.StatePaused,
            PlayStatus.Loading => PlaybackStateCompat.StateBuffering,
            PlayStatus.Stopped => PlaybackStateCompat.StatePaused,
            PlayStatus.Ended => PlaybackStateCompat.StatePaused,
            PlayStatus.Failed => PlaybackStateCompat.StateError,
            _ => PlaybackStateCompat.StateNone
        };
    }

    private static long GetCurrentPlaybackPosition(MediaSessionCompat session)
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

            // Skip stale position updates after playback has stopped/ended.
            // Without this, the last position update from the old playback arrives after
            // SetStoppedState and triggers an extra SetPlaybackState → button flash.
            var status = playbackState.Value.Status;
            if (status is PlayStatus.Stopped or PlayStatus.Ended)
            {
                return;
            }

            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                logger.Debug("[AndroidAuto] PlaybackPositionChangedMessage: MediaSession is null, skipping");
                return;
            }

            var duration = message.Duration ?? playbackState.Value.Duration;
            UpdatePlaybackPosition(message.CurrentPosition.Value, duration);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.AndroidMediaSessionCompatUpdateDiagnosticsLog.ErrorUpdatingPlaybackPosition);
        }
    }

    private void UpdatePlaybackPosition(TimeSpan currentPosition, TimeSpan duration)
    {
        var currentState = playbackState.Value;
        var canPlayNext = currentState.CanPlayNext;

        // Always enable previous button for Android Auto (even on first track - will restart current track)
        mediaSessionManager.UpdatePlaybackPosition(currentPosition, duration, canPlayNext, canPlayPrevious: true);
    }
}
