using AVFoundation;
using AVKit;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.MediaSource;
using CommunityToolkit.Maui.Primitives;
using CommunityToolkit.Maui.Views;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using Foundation;
using MediaPlayer;
using Microsoft.Extensions.Logging;
using UIKit;

namespace CommunityToolkit.Maui.Core.Views;

public partial class MediaManager : IDisposable
{
    Metadata? metaData;
    IosMediaManagerObserverCoordinator.Tokens? observerTokens;
    IDisposable? currentItemErrorObserver;

    // Media would still start playing when Speed was set although ShouldAutoPlay=False
    // This field was added to overcome that.
    bool isInitialSpeedSet;

    /// <summary>
    /// Creates the corresponding platform view of <see cref="MediaElement"/> on iOS and macOS.
    /// </summary>
    /// <returns>The platform native counterpart of <see cref="MediaElement"/>. PlayerViewController is null in headless mode.</returns>
    public (PlatformMediaElement Player, AVPlayerViewController? PlayerViewController) CreatePlatformView()
    {
        CreatePlayer();
        InitializePlayerProperties();
        SetupRemoteControlAndAudio();
        observerTokens = IosMediaManagerObserverCoordinator.Setup(
            Player!,
            MediaElement,
            Logger,
            NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New,
            () => metaData,
            () => PlayerItem,
            observerTokens);

        // In headless mode (audio-only), we don't need a PlayerViewController
        // AVPlayer works perfectly without a view controller for audio playback
        PlayerViewController = null;

        return (Player!, PlayerViewController);
    }

    void CreatePlayer()
    {
        Player = new();
        // PlayerViewController is not created in headless mode
        // AVPlayer can work without a view controller for audio-only playback
    }

    void InitializePlayerProperties()
    {
        // Pre-initialize Volume and Muted properties to the player object
        if (Player is null)
        {
            return;
        }

        Player.Muted = MediaElement.ShouldMute;
        var volumeDiff = Math.Abs(Player.Volume - MediaElement.Volume);
        if (volumeDiff > 0.01)
        {
            Player.Volume = (float)MediaElement.Volume;
        }
    }

    void SetupRemoteControlAndAudio()
    {
        UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();

        // Audio session must be configured even in headless mode for playback to work
        var avSession = AVAudioSession.SharedInstance();
        avSession.SetCategory(AVAudioSessionCategory.Playback);
        avSession.SetActive(true);

        // PlayerViewController-specific settings only apply when we have a view controller
        if (PlayerViewController is null)
        {
            return;
        }

#if IOS
        PlayerViewController.UpdatesNowPlayingInfoCenter = false;
#else
		PlayerViewController.UpdatesNowPlayingInfoCenter = true;
#endif
    }

    /// <summary>
    /// Releases the managed and unmanaged resources used by the <see cref="MediaManager"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The current media playback item.
    /// </summary>
    protected AVPlayerItem? PlayerItem { get; set; }

    /// <summary>
    /// The <see cref="AVPlayerViewController"/> that hosts the media Player.
    /// </summary>
    protected AVPlayerViewController? PlayerViewController { get; set; }

    protected virtual partial void PlatformPlay()
    {
        if (Player?.CurrentTime == PlayerItem?.Duration)
        {
            return;
        }

        Player?.Play();
    }

    protected virtual partial void PlatformPause()
    {
        Player?.Pause();
    }

    protected virtual async partial Task PlatformSeek(TimeSpan position, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var seekTaskCompletionSource = new TaskCompletionSource();

        if (Player?.CurrentItem is null)
        {
            throw new InvalidOperationException($"{nameof(AVPlayer)}.{nameof(AVPlayer.CurrentItem)} is not yet initialized");
        }

        if (Player.Status is not AVPlayerStatus.ReadyToPlay)
        {
            throw new InvalidOperationException($"{nameof(AVPlayer)}.{nameof(AVPlayer.Status)} must first be set to {AVPlayerStatus.ReadyToPlay}");
        }

        var ranges = Player.CurrentItem.SeekableTimeRanges;
        var seekToTime = new CMTime(Convert.ToInt64(position.TotalMilliseconds), 1000);
        bool seekPerformed = false;

        foreach (var range in ranges.Select(r => r.CMTimeRangeValue))
        {
            if (seekToTime >= range.Start && seekToTime < (range.Start + range.Duration))
            {
                Player.Seek(seekToTime, complete =>
                {
                    if (!complete)
                    {
                        seekTaskCompletionSource.SetException(new InvalidOperationException("Seek Failed"));
                        return;
                    }

                    seekTaskCompletionSource.SetResult();
                });
                seekPerformed = true;
                break;
            }
        }

        // If position is outside all seekable ranges, clamp to the nearest valid position
        if (!seekPerformed && ranges.Length > 0)
        {
            var firstRange = ranges[0].CMTimeRangeValue;
            var lastRange = ranges[ranges.Length - 1].CMTimeRangeValue;
            var lastRangeEnd = lastRange.Start + lastRange.Duration;

            CMTime clampedTime;
            if (seekToTime < firstRange.Start)
            {
                // Position is before first range - seek to start of first range
                clampedTime = firstRange.Start;
            }
            else if (seekToTime >= lastRangeEnd)
            {
                // Position is after last range - seek to end of last range
                clampedTime = lastRangeEnd;
            }
            else
            {
                // Position is between ranges - find the nearest range
                CMTime? nearestStart = null;
                CMTime? nearestEnd = null;
                double minDistance = double.MaxValue;

                foreach (var range in ranges.Select(r => r.CMTimeRangeValue))
                {
                    var rangeStart = range.Start;
                    var rangeEnd = range.Start + range.Duration;

                    if (seekToTime < rangeStart)
                    {
                        // Calculate distance in seconds using CMTime.Seconds property
                        var distance = seekToTime.Seconds - rangeStart.Seconds;
                        if (Math.Abs(distance) < Math.Abs(minDistance))
                        {
                            minDistance = distance;
                            nearestStart = rangeStart;
                        }
                    }
                    else if (seekToTime >= rangeEnd)
                    {
                        // Calculate distance in seconds using CMTime.Seconds property
                        var distance = seekToTime.Seconds - rangeEnd.Seconds;
                        if (Math.Abs(distance) < Math.Abs(minDistance))
                        {
                            minDistance = distance;
                            nearestEnd = rangeEnd;
                        }
                    }
                }

                clampedTime = nearestStart ?? nearestEnd ?? firstRange.Start;
            }

            Player.Seek(clampedTime, complete =>
            {
                if (!complete)
                {
                    seekTaskCompletionSource.SetException(new InvalidOperationException("Seek Failed"));
                    return;
                }

                seekTaskCompletionSource.SetResult();
            });
            seekPerformed = true;
        }

        if (!seekPerformed)
        {
            // No seekable ranges available - this can happen when media is still loading
            // Instead of throwing, just complete the seek task (effectively a no-op)
            // The caller can check the position after if needed
            // This allows playback to start from the beginning if seek isn't possible yet
            seekTaskCompletionSource.SetResult();
        }

        // Wait for seek to complete (or already completed if no ranges available)
        await seekTaskCompletionSource.Task.WaitAsync(token);

        // Always call SeekCompleted to notify listeners, even if seek was a no-op
        MediaElement.SeekCompleted();
    }

    protected virtual partial void PlatformStop()
    {
        // There's no Stop method so pause the video and reset its position
        Player?.Seek(CMTime.Zero);
        Player?.Pause();

        MediaElement.CurrentStateChanged(MediaElementState.Stopped);
    }

    protected virtual partial void PlatformUpdateAspect()
    {
        if (PlayerViewController is null)
        {
            return;
        }

        PlayerViewController.VideoGravity = MediaElement.Aspect switch
        {
            Aspect.Fill => AVLayerVideoGravity.Resize,
            Aspect.AspectFill => AVLayerVideoGravity.ResizeAspectFill,
            _ => AVLayerVideoGravity.ResizeAspect,
        };
    }

    protected virtual partial ValueTask PlatformUpdateSource()
    {
        MediaElement.CurrentStateChanged(MediaElementState.Opening);

        if (Player is null)
        {
            return ValueTask.CompletedTask;
        }
        var result = IosMediaManagerSourceUpdater.UpdateSource(
            MediaElement,
            Player,
            PlayerViewController,
            metaData,
            PlayerItem,
            currentItemErrorObserver,
            NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New,
            Logger);

        metaData = result.MetaData;
        PlayerItem = result.PlayerItem;
        currentItemErrorObserver = result.CurrentItemErrorObserver;
        return ValueTask.CompletedTask;
    }

    protected virtual partial void PlatformUpdateSpeed()
    {
        if (Player is null)
        {
            return;
        }

        // First time we're getting a playback speed and should NOT auto play, do nothing.
        if (!isInitialSpeedSet && !MediaElement.ShouldAutoPlay)
        {
            isInitialSpeedSet = true;
            return;
        }

        Player.Rate = (float)MediaElement.Speed;
    }

    protected virtual partial void PlatformUpdateShouldShowPlaybackControls()
    {
        if (PlayerViewController is null)
        {
            return;
        }

        PlayerViewController.ShowsPlaybackControls =
            MediaElement.ShouldShowPlaybackControls;
    }

    protected virtual partial void PlatformUpdatePosition()
    {
        if (Player is null)
        {
            return;
        }

        if (PlayerItem is not null)
        {
            if (PlayerItem.Duration == CMTime.Indefinite)
            {
                var range = PlayerItem.SeekableTimeRanges?.LastOrDefault();

                if (range?.CMTimeRangeValue is not null)
                {
                    MediaElement.Duration = ConvertTime(range.CMTimeRangeValue.Duration);
                    MediaElement.Position = ConvertTime(PlayerItem.CurrentTime);
                }
            }
            else
            {
                MediaElement.Duration = ConvertTime(PlayerItem.Duration);
                MediaElement.Position = ConvertTime(PlayerItem.CurrentTime);
            }
        }
        else
        {
            Player.Pause();
            MediaElement.Duration = MediaElement.Position = TimeSpan.Zero;
        }
    }

    protected virtual partial void PlatformUpdateVolume()
    {
        if (Player is null)
        {
            return;
        }

        var volumeDiff = Math.Abs(Player.Volume - MediaElement.Volume);
        if (volumeDiff > 0.01)
        {
            Player.Volume = (float)MediaElement.Volume;
        }
    }


    protected virtual partial void PlatformUpdateShouldKeepScreenOn()
    {
        if (Player is null)
        {
            return;
        }

        UIApplication.SharedApplication.IdleTimerDisabled = MediaElement.ShouldKeepScreenOn;
    }


    protected virtual partial void PlatformUpdateShouldMute()
    {
        if (Player is null)
        {
            return;
        }

        Player.Muted = MediaElement.ShouldMute;
    }

    protected virtual partial void PlatformUpdateShouldLoopPlayback()
    {
        // no-op we loop through using the PlayedToEndObserver
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="MediaManager"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            var player = Player;
            if (player is not null)
            {
                player.Pause();
                player.InvokeOnMainThread(UIApplication.SharedApplication.EndReceivingRemoteControlEvents);
                UIApplication.SharedApplication.IdleTimerDisabled = false;
                AVAudioSession.SharedInstance().SetActive(false);

                currentItemErrorObserver?.Dispose();
                currentItemErrorObserver = null;

                if (observerTokens is not null)
                {
                    IosMediaManagerObserverCoordinator.ReleaseTokens(observerTokens);
                    observerTokens = null;
                }

                player.ReplaceCurrentItemWithPlayerItem(null);
                player.Dispose();
                Player = null;
            }

            PlayerViewController?.Dispose();
            PlayerViewController = null;
        }
    }

    static TimeSpan ConvertTime(CMTime cmTime)
    {
        return TimeSpan.FromSeconds(double.IsNaN(cmTime.Seconds) ? 0 : cmTime.Seconds);
    }
}