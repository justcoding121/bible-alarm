#nullable enable
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Media;
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
    MediaSessionManager mediaSessionManager,
    IState<PlaybackState> playbackState,
    AndroidArtworkService artworkService) : IRecipient<PlaybackPositionChangedMessage>
{
    private static readonly ILogger logger = Log.ForContext<MediaSessionEffect>();

    public void RegisterMessageHandlers() => WeakReferenceMessenger.Default.Register(this);

    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            var session = GetValidatedSession("cannot update playback state");
            if (session == null)
            {
                return Task.CompletedTask;
            }

            // When status is Loading (during track preparation and media buffering),
            // always use SetBufferingStateOnly to preserve metadata and keep session active.
            // This prevents Android Auto from navigating away from the Now Playing screen.
            // The metadata will be updated when the new track's metadata arrives via PlaybackMetadataChangedAction.
            if (action.Status == PlayStatus.Loading)
            {
                // Always preserve metadata during Loading state to prevent Android Auto navigation
                // Whether it's fresh playback or track transition, keeping metadata prevents screen jumps
                mediaSessionManager.SetBufferingStateOnly();
                logger.Debug("Set MediaSession to buffering state only (preserving metadata to prevent navigation)");
            }
            else
            {
                // For all other statuses (Playing, Paused, Stopped, etc.), use normal state with controls
                var canPlayNext = playbackState.Value.CanPlayNext;
                // Always enable previous button for Android Auto (even on first track - will restart current track)
                var canPlayPrevious = true;
                mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext, canPlayPrevious);
            }

            // Save metadata to Preferences when playback starts (status changes to Playing)
            // This ensures we save as soon as user hits play and metadata is available
            if (action.Status == PlayStatus.Playing)
            {
                SaveCurrentMetadataToPreferencesIfAvailable();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat playback state");
        }

        return Task.CompletedTask;
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
    public Task HandleSetDefaultScheduleMetadata(SetDefaultScheduleMetadataAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Only update MediaSession if playback is not active
            // When playback is active, MediaSessionEffect.HandlePlaybackMetadataChanged handles updates
            if (playbackState.Value.IsPreparingOrPlaying)
            {
                logger.Debug("HandleSetDefaultScheduleMetadata: Playback is active, skipping default schedule metadata update");
                return Task.CompletedTask;
            }

            var session = GetValidatedSession("cannot update default schedule metadata");
            if (session == null)
            {
                return Task.CompletedTask;
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
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat with default schedule metadata");
        }

        return Task.CompletedTask;
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

            var state = MapPlayStatusToPlaybackState(playbackState.Value.Status);
            var position = GetCurrentPlaybackPosition(session);
            // Always enable previous button for Android Auto (even on first track - will restart current track)
            mediaSessionManager.UpdatePlaybackState(state, position, action.CanPlayNext, canPlayPrevious: true);
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
    /// </summary>
    public void Receive(PlaybackPositionChangedMessage message)
    {
        try
        {
            if (message.CurrentPosition == null)
            {
                return;
            }

            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                return;
            }

            UpdatePlaybackPosition(session, message.CurrentPosition.Value);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat playback position");
        }
    }

    private void UpdatePlaybackPosition(MediaSessionCompat session, TimeSpan currentPosition)
    {
        var duration = playbackState.Value.Duration;
        var canPlayNext = playbackState.Value.CanPlayNext;
        // Always enable previous button for Android Auto (even on first track - will restart current track)
        mediaSessionManager.UpdatePlaybackPosition(currentPosition, duration, canPlayNext, canPlayPrevious: true);
    }
}
