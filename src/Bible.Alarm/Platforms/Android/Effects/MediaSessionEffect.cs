#nullable enable
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
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
public class MediaSessionEffect
{
    private static readonly ILogger Logger = Log.ForContext<MediaSessionEffect>();
    private readonly MediaSessionManager _mediaSessionManager;

    public MediaSessionEffect(MediaSessionManager mediaSessionManager)
    {
        _mediaSessionManager = mediaSessionManager;
    }

    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            // Ensure MediaSession is created before accessing
            var session = _mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                Logger.Warning("MediaSessionCompat is null, cannot update playback state");
                return Task.CompletedTask;
            }

            // SetPlaybackStatus handles active state and audio focus automatically
            _mediaSessionManager.SetPlaybackStatus(action.Status);
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
            var session = _mediaSessionManager.GetOrCreate();
            if (session == null)
            {
                Logger.Warning("MediaSessionCompat is null, cannot update metadata");
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(action.Title) || !string.IsNullOrEmpty(action.Artist))
            {
                _mediaSessionManager.UpdateMetadata(
                    action.Title ?? "",
                    action.Artist ?? "",
                    action.Album);

                Logger.Debug("MediaSessionCompat metadata updated: Title={Title}, Artist={Artist}, Album={Album}",
                    action.Title, action.Artist, action.Album);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating MediaSessionCompat metadata");
        }

        return Task.CompletedTask;
    }
}
