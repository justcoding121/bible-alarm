#nullable enable
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
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

    public void RegisterMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(this);
    }

    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Ensure MediaSession is created before accessing
            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                logger.Warning("MediaSessionCompat is null, cannot update playback state");
                return Task.CompletedTask;
            }

            // Get navigation availability from current playback state
            var canPlayNext = playbackState.Value.CanPlayNext;
            var canPlayPrevious = playbackState.Value.CanPlayPrevious;

            // SetPlaybackStatus handles active state and audio focus automatically
            mediaSessionManager.SetPlaybackStatus(action.Status, canPlayNext, canPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat playback state");
        }

        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task HandlePlaybackMetadataChanged(PlaybackMetadataChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Ensure MediaSession is created before accessing
            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                logger.Warning("MediaSessionCompat is null, cannot update metadata");
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist))
            {
                // Update metadata with scheduleId from state for OnPlayFromMediaId
                var metadataBuilder = new MediaMetadataCompat.Builder();
                if (metadataBuilder != null)
                {
                    metadataBuilder.PutString(MediaMetadataCompat.MetadataKeyTitle, action.Title ?? "");
                    metadataBuilder.PutString(MediaMetadataCompat.MetadataKeyArtist, action.Artist ?? "");
                    metadataBuilder.PutString(MediaMetadataCompat.MetadataKeyAlbum, action.Album ?? "");

                    // Include scheduleId from state as mediaId for OnPlayFromMediaId callback
                    var scheduleId = playbackState.Value?.CurrentScheduleId;
                    if (scheduleId.HasValue)
                    {
                        metadataBuilder.PutString(MediaMetadataCompat.MetadataKeyMediaId,
                            scheduleId.Value.ToString());
                    }

                    // Load and set artwork bitmap if available
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

                    var metadata = metadataBuilder.Build();
                    session?.SetMetadata(metadata);
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
    public Task HandlePlaybackDurationChanged(PlaybackDurationChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Ensure MediaSession is created before accessing
            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                logger.Warning("MediaSessionCompat is null, cannot update duration");
                return Task.CompletedTask;
            }

            // Update duration in metadata
            var durationMs = (long)action.Duration.TotalMilliseconds;
            if (durationMs > 0)
            {
                var currentMetadata = session.Controller?.Metadata;
                if (currentMetadata != null)
                {
                    var metadataBuilder = new MediaMetadataCompat.Builder(currentMetadata);
                    metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, durationMs);
                    session.SetMetadata(metadataBuilder.Build());
                }
            }
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
            // Ensure MediaSession is created before accessing
            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                logger.Warning("MediaSessionCompat is null, cannot update navigation state");
                return Task.CompletedTask;
            }

            // Get current playback state to update with new navigation availability
            var currentState = playbackState.Value;
            var state = currentState.Status switch
            {
                PlayStatus.Playing => PlaybackStateCompat.StatePlaying,
                PlayStatus.Paused => PlaybackStateCompat.StatePaused,
                PlayStatus.Loading => PlaybackStateCompat.StateBuffering,
                PlayStatus.Stopped => PlaybackStateCompat.StateStopped,
                PlayStatus.Ended => PlaybackStateCompat.StateStopped,
                PlayStatus.Failed => PlaybackStateCompat.StateError,
                _ => PlaybackStateCompat.StateNone
            };

            // Update playback state with new navigation availability
            var playbackStateCompat = session.Controller?.PlaybackState;
            var position = playbackStateCompat?.Position ?? 0;
            mediaSessionManager.UpdatePlaybackState(state, position, action.CanPlayNext, action.CanPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat navigation state");
        }

        return Task.CompletedTask;
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

            // Ensure MediaSession is created before accessing
            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                return;
            }

            // Get duration and navigation availability from playback state
            var duration = playbackState.Value.Duration;
            var canPlayNext = playbackState.Value.CanPlayNext;
            var canPlayPrevious = playbackState.Value.CanPlayPrevious;

            // Update position in MediaSessionCompat
            mediaSessionManager.UpdatePlaybackPosition(message.CurrentPosition.Value, duration, canPlayNext, canPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating MediaSessionCompat playback position");
        }
    }
}
