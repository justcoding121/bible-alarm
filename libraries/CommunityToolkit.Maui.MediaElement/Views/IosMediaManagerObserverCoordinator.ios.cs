#nullable enable

using AVFoundation;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.Primitives;
using CoreFoundation;
using CoreMedia;
using Foundation;
using MediaPlayer;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Maui.Views;

internal sealed class IosMediaManagerObserverCoordinator
{
    internal sealed class Tokens
    {
        public IDisposable? RateObserver { get; set; }
        public IDisposable? StatusObserver { get; set; }
        public IDisposable? TimeControlStatusObserver { get; set; }
        public IDisposable? VolumeObserver { get; set; }
        public IDisposable? MutedObserver { get; set; }

        public NSObject? ErrorObserver { get; set; }
        public NSObject? ItemFailedToPlayToEndTimeObserver { get; set; }
        public NSObject? PlaybackStalledObserver { get; set; }
        public NSObject? PlayedToEndObserver { get; set; }
    }

    internal static Tokens Setup(
        AVPlayer player,
        IMediaElement mediaElement,
        ILogger logger,
        NSKeyValueObservingOptions valueObserverOptions,
        Func<Metadata?> getMetaData,
        Func<AVPlayerItem?> getPlayerItem,
        Tokens? existing = null)
    {
        if (existing is not null)
        {
            Dispose(existing);
        }

        var tokens = new Tokens();

        tokens.MutedObserver = player.AddObserver("muted", valueObserverOptions, _ =>
        {
            mediaElement.ShouldMute = player.Muted;
        });

        tokens.VolumeObserver = player.AddObserver("volume", valueObserverOptions, _ =>
        {
            var volumeDiff = Math.Abs(player.Volume - mediaElement.Volume);
            if (volumeDiff > 0.01)
            {
                mediaElement.Volume = player.Volume;
            }
        });

        tokens.StatusObserver = player.AddObserver("status", valueObserverOptions, _ =>
        {
            var newState = player.Status switch
            {
                AVPlayerStatus.Unknown => MediaElementState.Stopped,
                AVPlayerStatus.ReadyToPlay => MediaElementState.Paused,
                AVPlayerStatus.Failed => MediaElementState.Failed,
                _ => mediaElement.CurrentState
            };

            mediaElement.CurrentStateChanged(newState);
        });

        tokens.TimeControlStatusObserver = player.AddObserver("timeControlStatus", valueObserverOptions, _ =>
        {
            if (player.Status is AVPlayerStatus.Unknown || player.CurrentItem?.Error is not null)
            {
                return;
            }

            var newState = player.TimeControlStatus switch
            {
                AVPlayerTimeControlStatus.Paused => MediaElementState.Paused,
                AVPlayerTimeControlStatus.Playing => MediaElementState.Playing,
                AVPlayerTimeControlStatus.WaitingToPlayAtSpecifiedRate => MediaElementState.Buffering,
                _ => mediaElement.CurrentState
            };

            getMetaData()?.SetMetadata(getPlayerItem(), mediaElement);
            mediaElement.CurrentStateChanged(newState);
        });

        tokens.RateObserver = AVPlayer.Notifications.ObserveRateDidChange((sender, args) =>
        {
            if (!AreFloatingPointNumbersEqual(mediaElement.Speed, player.Rate))
            {
                mediaElement.Speed = player.Rate;
                var metaData = getMetaData();
                if (metaData is not null)
                {
                    metaData.NowPlayingInfo.PlaybackRate = (float)mediaElement.Speed;
                    MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = metaData.NowPlayingInfo;
                }
            }
        });

        tokens.ItemFailedToPlayToEndTimeObserver = AVPlayerItem.Notifications.ObserveItemFailedToPlayToEndTime((sender, args) =>
            ErrorOccurred(player, mediaElement, logger, sender, args));
        tokens.PlaybackStalledObserver = AVPlayerItem.Notifications.ObservePlaybackStalled((sender, args) =>
            ErrorOccurred(player, mediaElement, logger, sender, args));
        tokens.ErrorObserver = AVPlayerItem.Notifications.ObserveNewErrorLogEntry((sender, args) =>
            ErrorOccurred(player, mediaElement, logger, sender, args));

        tokens.PlayedToEndObserver = AVPlayerItem.Notifications.ObserveDidPlayToEndTime((sender, args) =>
            PlayedToEnd(player, mediaElement, logger, sender, args));

        return tokens;
    }

    internal static void Dispose(Tokens tokens)
    {
        tokens.RateObserver?.Dispose();
        tokens.RateObserver = null;

        tokens.StatusObserver?.Dispose();
        tokens.StatusObserver = null;

        tokens.TimeControlStatusObserver?.Dispose();
        tokens.TimeControlStatusObserver = null;

        tokens.VolumeObserver?.Dispose();
        tokens.VolumeObserver = null;

        tokens.MutedObserver?.Dispose();
        tokens.MutedObserver = null;

        tokens.ItemFailedToPlayToEndTimeObserver?.Dispose();
        tokens.ItemFailedToPlayToEndTimeObserver = null;

        tokens.PlaybackStalledObserver?.Dispose();
        tokens.PlaybackStalledObserver = null;

        tokens.ErrorObserver?.Dispose();
        tokens.ErrorObserver = null;

        tokens.PlayedToEndObserver?.Dispose();
        tokens.PlayedToEndObserver = null;
    }

    private static void ErrorOccurred(AVPlayer player, IMediaElement mediaElement, ILogger logger, object? sender, NSNotificationEventArgs args)
    {
        var error = player.CurrentItem?.Error;
        if (error is not null)
        {
            var message = error.LocalizedDescription;
            mediaElement.MediaFailed(new MediaFailedEventArgs(message));
            logger.LogError("{LogMessage}", message);
            return;
        }

        var nonFatal = args.Notification?.ToString() ?? "Media playback failed for an unknown reason.";
        logger.LogWarning("{LogMessage}", nonFatal);
    }

    private static void PlayedToEnd(AVPlayer player, IMediaElement mediaElement, ILogger logger, object? sender, NSNotificationEventArgs args)
    {
        if (args.Notification.Object != player.CurrentItem)
        {
            return;
        }

        if (mediaElement.ShouldLoopPlayback)
        {
            player.Seek(CMTime.Zero);
            player.Play();
            return;
        }

        try
        {
            DispatchQueue.MainQueue.DispatchAsync(mediaElement.MediaEnded);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "{LogMessage}", "Failed to play media to end.");
        }
    }

    private static bool AreFloatingPointNumbersEqual(in double number1, in double number2, double tolerance = 0.01)
        => Math.Abs(number1 - number2) > tolerance;
}

