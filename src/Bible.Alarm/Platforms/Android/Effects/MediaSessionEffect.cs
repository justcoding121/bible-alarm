#nullable enable
using Android.Support.V4.Media;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
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
    AndroidArtworkService artworkService)
{
    private static readonly ILogger Logger = Log.ForContext<MediaSessionEffect>();

    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Ensure MediaSession is created before accessing
            var session = mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                Logger.Warning("MediaSessionCompat is null, cannot update playback state");
                return Task.CompletedTask;
            }

            // SetPlaybackStatus handles active state and audio focus automatically
            mediaSessionManager.SetPlaybackStatus(action.Status);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating MediaSessionCompat playback state");
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
                Logger.Warning("MediaSessionCompat is null, cannot update metadata");
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist))
            {
                // Update metadata with scheduleId from state for OnPlayFromMediaId
                var metadataBuilder = new MediaMetadataCompat.Builder()
                    .PutString(MediaMetadataCompat.MetadataKeyTitle, action.Title ?? "")
                    .PutString(MediaMetadataCompat.MetadataKeyArtist, action.Artist ?? "")
                    .PutString(MediaMetadataCompat.MetadataKeyAlbum, action.Album ?? "");
                
                // Include scheduleId from state as mediaId for OnPlayFromMediaId callback
                var scheduleId = playbackState.Value.CurrentScheduleId;
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
                        var artworkBitmap = artworkService.LoadArtworkBitmap(action.ArtworkUrl);
                        if (artworkBitmap != null)
                        {
                            metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artworkBitmap);
                            Logger.Debug("MediaSessionCompat artwork set from: {ArtworkUrl}", action.ArtworkUrl);
                        }
                        else
                        {
                            Logger.Debug("Failed to load artwork bitmap from: {ArtworkUrl}", action.ArtworkUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", action.ArtworkUrl);
                    }
                }
                
                if (scheduleId.HasValue)
                {
                    Logger.Debug("MediaSessionCompat metadata updated with ScheduleId from state: Title={Title}, Artist={Artist}, Album={Album}, ScheduleId={ScheduleId}, HasArtwork={HasArtwork}",
                        action.Title, action.Artist, action.Album, scheduleId.Value, !string.IsNullOrEmpty(action.ArtworkUrl));
                }
                else
                {
                    Logger.Debug("MediaSessionCompat metadata updated: Title={Title}, Artist={Artist}, Album={Album}, HasArtwork={HasArtwork}",
                        action.Title, action.Artist, action.Album, !string.IsNullOrEmpty(action.ArtworkUrl));
                }
                
                session.SetMetadata(metadataBuilder.Build());
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating MediaSessionCompat metadata");
        }

        return Task.CompletedTask;
    }
}
