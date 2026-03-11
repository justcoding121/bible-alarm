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
            ReleaseTokens(existing);
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

            mediaElement.CurrentStateChanged(newState);
        });

        tokens.RateObserver = AVPlayer.Notifications.ObserveRateDidChange((sender, args) =>
        {
            if (!AreFloatingPointNumbersEqual(mediaElement.Speed, player.Rate))
            {
                mediaElement.Speed = player.Rate;
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

    internal static void ReleaseTokens(Tokens tokens)
    {
        DisposeAndSuppressFinalize(tokens.RateObserver);
        tokens.RateObserver = null;

        DisposeAndSuppressFinalize(tokens.StatusObserver);
        tokens.StatusObserver = null;

        DisposeAndSuppressFinalize(tokens.TimeControlStatusObserver);
        tokens.TimeControlStatusObserver = null;

        DisposeAndSuppressFinalize(tokens.VolumeObserver);
        tokens.VolumeObserver = null;

        DisposeAndSuppressFinalize(tokens.MutedObserver);
        tokens.MutedObserver = null;

        DisposeAndSuppressFinalize(tokens.ItemFailedToPlayToEndTimeObserver);
        tokens.ItemFailedToPlayToEndTimeObserver = null;

        DisposeAndSuppressFinalize(tokens.PlaybackStalledObserver);
        tokens.PlaybackStalledObserver = null;

        DisposeAndSuppressFinalize(tokens.ErrorObserver);
        tokens.ErrorObserver = null;

        DisposeAndSuppressFinalize(tokens.PlayedToEndObserver);
        tokens.PlayedToEndObserver = null;
    }

    /// <summary>
    /// Disposes an observer and suppresses its GC finalizer to prevent
    /// SIGSEGV crashes from objc_msgSend to freed native objects.
    /// KVO observers (IDisposable) and notification observers (NSObject)
    /// both wrap native ObjC objects that can be deallocated before finalization.
    /// </summary>
    private static void DisposeAndSuppressFinalize(IDisposable? obj)
    {
        if (obj == null)
        {
            return;
        }

        try
        {
            obj.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        GC.SuppressFinalize(obj);
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

