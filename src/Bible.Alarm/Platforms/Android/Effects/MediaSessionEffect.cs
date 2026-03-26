#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
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
            var session = GetValidatedSession("cannot update playback state");
            if (session == null)
            {
                return;
            }

            var currentState = playbackState.Value;
            var isAutoAdvancing = currentState.IsAutoAdvancing;
            var previousStatus = currentState.Status;

            if (isRestartingPlayback)
            {
                if (action.Status is PlayStatus.Stopped or PlayStatus.Ended)
                {
                    logger.Information(
                        "[AndroidAuto] Suppressing {Status} MediaSession update during playback restart to prevent flashing",
                        action.Status);
                    ForegroundServiceCoordinator.OnPlaybackStopped();
                    return;
                }

                isRestartingPlayback = false;
                logger.Information(
                    "[AndroidAuto] Cleared restart suppression flag on {Status} status", action.Status);
            }

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
                // Fluxor effects for the same action type can run concurrently.
                // If Loading and Playing are dispatched close together, the Playing
                // effect may set StatePlaying before this Loading effect runs.
                // Re-check the current Fluxor status to avoid overwriting Playing
                // with Buffering, which would leave the progress animation stuck.
                if (playbackState.Value.Status != PlayStatus.Loading)
                {
                    logger.Debug(
                        "[AndroidAuto] Skipping Buffering state — Fluxor status already advanced past Loading to {Status}",
                        playbackState.Value.Status);
                    return;
                }

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
            else if (action.Status == PlayStatus.Stopped)
            {
                // Re-read isAutoAdvancing from the current Fluxor state, not the snapshot captured
                // at the top of this method. During auto-advance, StateChanged(Stopped) fires before
                // MediaEnded dispatches SetAutoAdvancingAction(true). The reducer runs synchronously,
                // so by the time this async effect executes, isAutoAdvancing may have been updated.
                var currentAutoAdvancing = playbackState.Value.IsAutoAdvancing;
                if (currentAutoAdvancing)
                {
                    var canPlayNext = true;
                    var canPlayPrevious = true;
                    logger.Information(
                        "[AndroidAuto] Stopped status with auto-advancing: Setting to Playing state (pause button visible, no prev/next buttons during transition)");
                    mediaSessionManager.SetPlaybackStatus(PlayStatus.Playing, canPlayNext, canPlayPrevious);
                }
                else
                {
                    var canPlayNext = true;
                    var canPlayPrevious = true;
                    logger.Information(
                        "[AndroidAuto] Setting playback status to {Status} - CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                        action.Status,
                        canPlayNext,
                        canPlayPrevious);
                    mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext, canPlayPrevious);
                }
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
                ResetMetadataDedup();
            }

            // On Android 13+, every active MediaSession gets a system-generated notification.
            // Deactivate the legacy MediaSessionCompat when MediaElement handles playback
            // and Android Auto is not connected to prevent a duplicate notification.
            // The Media3 MediaSession from ExoPlayer handles all system controls on its own.
            if (!ForegroundServiceCoordinator.IsAndroidAutoConnected
                && ForegroundServiceCoordinator.CurrentOwner == ForegroundServiceCoordinator.ForegroundServiceOwner.MediaElement)
            {
                mediaSessionManager.SetActive(false);
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

    private static global::Android.Graphics.Bitmap? cachedAppIconBitmap;

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
                logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", action.ArtworkUrl);
            }
        }

        // No new artwork - preserve existing artwork from the MediaSession to prevent
        // blank artwork during transitions (while async artwork extraction is pending).
        var existingArtwork = session.Controller?.Metadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
        if (existingArtwork != null)
        {
            metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
            return;
        }

        // No existing artwork (fresh start) - use app icon as fallback
        var fallback = GetOrLoadAppIconBitmap();
        if (fallback != null)
        {
            metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, fallback);
        }
    }

    private static global::Android.Graphics.Bitmap? GetOrLoadAppIconBitmap()
    {
        if (cachedAppIconBitmap != null)
        {
            return cachedAppIconBitmap;
        }

        try
        {
            var context = global::Android.App.Application.Context;
            var drawable = ContextCompat.GetDrawable(
                context, ResourceConstant.Drawable.ic_launcher_round);
            if (drawable == null)
            {
                return null;
            }

            var width = drawable.IntrinsicWidth > 0 ? drawable.IntrinsicWidth : 512;
            var height = drawable.IntrinsicHeight > 0 ? drawable.IntrinsicHeight : 512;
            var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(
                width, height, global::Android.Graphics.Bitmap.Config.Argb8888!);
            if (bitmap == null)
            {
                return null;
            }

            var canvas = new global::Android.Graphics.Canvas(bitmap);
            drawable.SetBounds(0, 0, width, height);
            drawable.Draw(canvas);
            cachedAppIconBitmap = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[AndroidAuto] Error loading app icon fallback artwork");
            return null;
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
            logger.Error(ex, "Error updating MediaSessionCompat navigation state");
        }

        return Task.CompletedTask;
    }

    private int MapPlayStatusToPlaybackState(PlayStatus status)
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
