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

        if (Player?.CurrentItem is null || Player.Status is not AVPlayerStatus.ReadyToPlay)
        {
            MediaElement.SeekCompleted();
            return;
        }

        var ranges = Player.CurrentItem.SeekableTimeRanges;
        var seekToTime = new CMTime(Convert.ToInt64(position.TotalMilliseconds), 1000);
        var rangeValues = ranges.Select(r => r.CMTimeRangeValue).ToArray();

        var seekPerformed = TrySeekInsideSeekableRanges(Player, seekToTime, rangeValues, seekTaskCompletionSource);

        if (!seekPerformed && rangeValues.Length > 0)
        {
            var clampedTime = ComputeClampedSeekTime(seekToTime, rangeValues);
            SeekAndComplete(Player, clampedTime, seekTaskCompletionSource);
            seekPerformed = true;
        }

        if (!seekPerformed)
        {
            seekTaskCompletionSource.SetResult();
        }

        await seekTaskCompletionSource.Task.WaitAsync(token);

        MediaElement.SeekCompleted();
    }

    static bool TrySeekInsideSeekableRanges(
        AVPlayer player,
        CMTime seekToTime,
        CMTimeRange[] rangeValues,
        TaskCompletionSource seekTaskCompletionSource)
    {
        foreach (var range in rangeValues)
        {
            if (seekToTime >= range.Start && seekToTime < (range.Start + range.Duration))
            {
                SeekAndComplete(player, seekToTime, seekTaskCompletionSource);
                return true;
            }
        }

        return false;
    }

    static void SeekAndComplete(AVPlayer player, CMTime time, TaskCompletionSource seekTaskCompletionSource)
    {
        player.Seek(time, complete =>
        {
            // Seek can return !complete for non-fatal reasons (interrupted by
            // another seek, media still loading, etc.). Treat as success to
            // avoid crashing the app with an unhandled exception.
            seekTaskCompletionSource.SetResult();
        });
    }

    static CMTime ComputeClampedSeekTime(CMTime seekToTime, CMTimeRange[] ranges)
    {
        var firstRange = ranges[0];
        var lastRange = ranges[ranges.Length - 1];
        var lastRangeEnd = lastRange.Start + lastRange.Duration;

        if (seekToTime < firstRange.Start)
        {
            return firstRange.Start;
        }

        if (seekToTime >= lastRangeEnd)
        {
            return lastRangeEnd;
        }

        CMTime? nearestStart = null;
        CMTime? nearestEnd = null;
        double minDistance = double.MaxValue;

        foreach (var range in ranges)
        {
            var rangeStart = range.Start;
            var rangeEnd = range.Start + range.Duration;

            if (seekToTime < rangeStart)
            {
                var distance = seekToTime.Seconds - rangeStart.Seconds;
                if (Math.Abs(distance) < Math.Abs(minDistance))
                {
                    minDistance = distance;
                    nearestStart = rangeStart;
                }
            }
            else if (seekToTime >= rangeEnd)
            {
                var distance = seekToTime.Seconds - rangeEnd.Seconds;
                if (Math.Abs(distance) < Math.Abs(minDistance))
                {
                    minDistance = distance;
                    nearestEnd = rangeEnd;
                }
            }
        }

        return nearestStart ?? nearestEnd ?? firstRange.Start;
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
            new IosMediaSourcePlayerContext(MediaElement, Player, PlayerViewController),
            new IosMediaSourceItemState(
                metaData,
                PlayerItem,
                currentItemErrorObserver,
                NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New,
                Logger));

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
        if (!disposing)
        {
            return;
        }

        var player = Player;
        var playerViewController = PlayerViewController;

        metaData?.Cleanup();
        metaData = null;

        if (player is not null)
        {
            TearDownPlayerForDispose(player);
            Player = null;
        }

        PlayerItem = null;
        playerViewController?.Dispose();
        PlayerViewController = null;
    }

    void TearDownPlayerForDispose(AVPlayer player)
    {
        player.Pause();
        player.InvokeOnMainThread(UIApplication.SharedApplication.EndReceivingRemoteControlEvents);
        UIApplication.SharedApplication.IdleTimerDisabled = false;
        AVAudioSession.SharedInstance().SetActive(false);

        DisposeCurrentItemErrorObserverSafely();

        if (observerTokens is not null)
        {
            IosMediaManagerObserverCoordinator.ReleaseTokens(observerTokens);
            observerTokens = null;
        }

        player.ReplaceCurrentItemWithPlayerItem(null);
        player.Dispose();
    }

    void DisposeCurrentItemErrorObserverSafely()
    {
        var errorObserver = currentItemErrorObserver;
        currentItemErrorObserver = null;
        if (errorObserver is null)
        {
            return;
        }

        try
        {
            errorObserver.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // KVO teardown can throw if already finalized; intentional no-op.
        }
    }

    static TimeSpan ConvertTime(CMTime cmTime)
    {
        return TimeSpan.FromSeconds(double.IsNaN(cmTime.Seconds) ? 0 : cmTime.Seconds);
    }
}