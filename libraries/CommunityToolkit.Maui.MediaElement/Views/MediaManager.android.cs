using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Android.OS;
using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Text;
using AndroidX.Media3.Common.Util;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using AndroidX.Media3.UI;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.MediaSource;
using CommunityToolkit.Maui.Primitives;
using CommunityToolkit.Maui.Services;
using CommunityToolkit.Maui.Views;
using Java.Lang;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;
using AudioAttributes = AndroidX.Media3.Common.AudioAttributes;
using DeviceInfo = AndroidX.Media3.Common.DeviceInfo;
using Exception = System.Exception;
using MediaMetadata = AndroidX.Media3.Common.MediaMetadata;
using Object = Java.Lang.Object;

namespace CommunityToolkit.Maui.Core.Views;

public partial class MediaManager : Object, IPlayerListener
{
    const int bufferState = 2;
    const int readyState = 3;
    const int endedState = 4;

    readonly SemaphoreSlim seekToSemaphoreSlim = new(1, 1);

    double? previousSpeed;
    float volumeBeforeMute = 1;

    TaskCompletionSource? seekToTaskCompletionSource;
    CancellationTokenSource? cancellationTokenSource;
    MediaSession? session;
    MediaItem.Builder? mediaItem;
    BoundServiceConnection? connection;

    public MediaSession? Session => session;

    protected PlayerView? PlayerView { get; set; }

    public void OnPlaybackParametersChanged(PlaybackParameters? playbackParameters)
    {
        if (playbackParameters is null || AreFloatingPointNumbersEqual(playbackParameters.Speed, MediaElement.Speed))
        {
            return;
        }

        MediaElement.Speed = playbackParameters.Speed;
    }

    public void UpdateNotifications()
    {
        if (connection?.Binder?.Service is null)
        {
#if DEBUG
            Logger.LogDebug("Notification Service not running.");
#endif
            return;
        }

        if (session is not null && Player is not null)
        {
            connection.Binder.Service.UpdateNotifications(session, Player);
        }
    }

    public void OnPlayerStateChanged(bool playWhenReady, int playbackState)
    {
        if (Player is null || MediaElement.Source is null)
        {
            return;
        }

        var newState = playbackState switch
        {
            PlaybackState.StateFastForwarding
                or PlaybackState.StateRewinding
                or PlaybackState.StateSkippingToNext
                or PlaybackState.StateSkippingToPrevious
                or PlaybackState.StateSkippingToQueueItem
                or PlaybackState.StatePlaying => playWhenReady
                    ? MediaElementState.Playing
                    : MediaElementState.Paused,

            PlaybackState.StatePaused => MediaElementState.Paused,

            PlaybackState.StateConnecting
                or PlaybackState.StateBuffering => MediaElementState.Buffering,

            PlaybackState.StateNone => MediaElementState.None,
            PlaybackState.StateStopped => MediaElement.CurrentState is not MediaElementState.Failed
                ? MediaElementState.Stopped
                : MediaElementState.Failed,

            PlaybackState.StateError => MediaElementState.Failed,

            _ => MediaElementState.None,
        };

        MediaElement.CurrentStateChanged(newState);
        if (playbackState is readyState)
        {
            MediaElement.Duration = TimeSpan.FromMilliseconds(Player.Duration < 0 ? 0 : Player.Duration);
            MediaElement.Position = TimeSpan.FromMilliseconds(Player.CurrentPosition < 0 ? 0 : Player.CurrentPosition);
        }
    }

    [MemberNotNull(nameof(Player), nameof(session))]
    public (PlatformMediaElement platformView, PlayerView? PlayerView) CreatePlatformView(AndroidViewType androidViewType)
    {
        AndroidMainThreadRunner.RunOnMainThread(() =>
        {
            (Player, session) = AndroidGlobalExoPlayerFactory.CreateOrReuse(MauiContext, this);
        });

        // Always headless mode - no PlayerView needed for audio-only playback
        PlayerView? playerView = null;
        // Player and session are guaranteed to be non-null due to [MemberNotNull] attribute and initialization in RunOnMainThread
        if (Player == null || session == null)
        {
            throw new InvalidOperationException("Player or session is null after initialization");
        }
        return (Player, playerView);
    }

    public void OnPlaybackStateChanged(int playbackState)
    {
        if (MediaElement.Source is null)
        {
            return;
        }

        MediaElementState newState = MediaElement.CurrentState;
        switch (playbackState)
        {
            case bufferState:
                newState = MediaElementState.Buffering;
                break;
            case endedState:
                newState = MediaElementState.Stopped;
                MediaElement.MediaEnded();
                break;
            case readyState:
                seekToTaskCompletionSource?.TrySetResult();
                break;
        }

        MediaElement.CurrentStateChanged(newState);
    }

    public void OnPlayerError(PlaybackException? error)
    {
        var errorMessage = string.Empty;
        var errorCode = string.Empty;
        var errorCodeName = string.Empty;

        if (!string.IsNullOrWhiteSpace(error?.LocalizedMessage))
        {
            errorMessage = $"Error message: {error.LocalizedMessage}";
        }

        if (error?.ErrorCode is not null)
        {
            errorCode = $"Error code: {error.ErrorCode}";
        }

        if (!string.IsNullOrWhiteSpace(error?.ErrorCodeName))
        {
            errorCodeName = $"Error codename: {error.ErrorCodeName}";
        }

        var message = string.Join(", ", new[]
        {
            errorCodeName,
            errorCode,
            errorMessage
        }.Where(static s => !string.IsNullOrEmpty(s)));

        MediaElement.MediaFailed(new MediaFailedEventArgs(message));

        Logger.LogError("{LogMessage}", message);
    }

    public void OnVideoSizeChanged(VideoSize? videoSize)
    {
        MediaElement.MediaWidth = videoSize?.Width ?? 0;
        MediaElement.MediaHeight = videoSize?.Height ?? 0;
    }

    public void OnVolumeChanged(float volume)
    {
        if (Player is null)
        {
            return;
        }

        // When currently muted, ignore
        if (MediaElement.ShouldMute)
        {
            return;
        }

        MediaElement.Volume = volume;
    }

    protected virtual partial void PlatformPlay()
    {
        if (Player is null || MediaElement.Source is null)
        {
            return;
        }

        Player.Prepare();
        Player.Play();
    }

    protected virtual partial void PlatformPause()
    {
        if (Player is null || MediaElement.Source is null)
        {
            return;
        }

        Player.Pause();
    }

    [MemberNotNull(nameof(Player))]
    protected virtual async partial Task PlatformSeek(TimeSpan position, CancellationToken token)
    {
        if (Player is null)
        {
            throw new InvalidOperationException($"{nameof(IExoPlayer)} is not yet initialized");
        }

        await seekToSemaphoreSlim.WaitAsync(token);

        seekToTaskCompletionSource = new();
        try
        {
            Player.SeekTo((long)position.TotalMilliseconds);

            // Here, we don't want to throw an exception
            // and to keep the execution on the thread that called this method
            await seekToTaskCompletionSource.Task.WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);

            MediaElement.SeekCompleted();
        }
        finally
        {
            seekToSemaphoreSlim.Release();
        }
    }

    protected virtual partial void PlatformStop()
    {
        if (Player is null || MediaElement.Source is null)
        {
            return;
        }

        Player.SeekTo(0);
        Player.Stop();
        MediaElement.Position = TimeSpan.Zero;
    }

    protected virtual async partial ValueTask PlatformUpdateSource()
    {
        if (Player is null)
        {
            return;
        }

        if (connection is null)
        {
            StartService();
        }

        (cancellationTokenSource, mediaItem) = await AndroidMediaManagerSourceUpdater.UpdateSourceAsync(
            MediaElement,
            Player,
            PlayerView,
            cancellationTokenSource,
            mediaItem,
            UpdateNotifications,
            Logger).ConfigureAwait(true);
    }

    protected virtual partial void PlatformUpdateAspect()
    {
        if (PlayerView is null)
        {
            return;
        }

        PlayerView.ResizeMode = MediaElement.Aspect switch
        {
            Aspect.AspectFill => AspectRatioFrameLayout.ResizeModeZoom,
            Aspect.Fill => AspectRatioFrameLayout.ResizeModeFill,
            Aspect.Center or Aspect.AspectFit => AspectRatioFrameLayout.ResizeModeFit,
            _ => throw new NotSupportedException($"{nameof(Aspect)}: {MediaElement.Aspect} is not yet supported")
        };
    }

    protected virtual partial void PlatformUpdateSpeed()
    {
        if (Player is null)
        {
            return;
        }

        // First time we're getting a playback speed, set initial value
        previousSpeed ??= MediaElement.Speed;

        if (MediaElement.Speed > 0)
        {
            Player.SetPlaybackSpeed((float)MediaElement.Speed);

            if (previousSpeed is 0)
            {
                Player.Play();
            }

            previousSpeed = MediaElement.Speed;
        }
        else
        {
            previousSpeed = 0;
            Player.Pause();
        }
    }

    protected virtual partial void PlatformUpdateShouldShowPlaybackControls()
    {
        if (PlayerView is null)
        {
            return;
        }

        PlayerView.UseController = MediaElement.ShouldShowPlaybackControls;
    }

    protected virtual partial void PlatformUpdatePosition()
    {
        if (Player is null)
        {
            return;
        }

        if (MediaElement.Duration != TimeSpan.Zero)
        {
            MediaElement.Position = TimeSpan.FromMilliseconds(Player.CurrentPosition);
        }
    }

    protected virtual partial void PlatformUpdateVolume()
    {
        if (Player is null)
        {
            return;
        }

        // If the user changes while muted, change the internal field
        // and do not update the actual volume.
        if (MediaElement.ShouldMute)
        {
            volumeBeforeMute = (float)MediaElement.Volume;
            return;
        }

        Player.Volume = (float)MediaElement.Volume;
    }

    protected virtual partial void PlatformUpdateShouldKeepScreenOn()
    {
        if (PlayerView is null)
        {
            return;
        }

        PlayerView.KeepScreenOn = MediaElement.ShouldKeepScreenOn;
    }

    protected virtual partial void PlatformUpdateShouldMute()
    {
        if (Player is null)
        {
            return;
        }

        // We're going to mute state. Capture the current volume first so we can restore later.
        if (MediaElement.ShouldMute)
        {
            volumeBeforeMute = Player.Volume;
        }
        else if (!AreFloatingPointNumbersEqual(volumeBeforeMute, Player.Volume) && Player.Volume > 0)
        {
            volumeBeforeMute = Player.Volume;
        }

        Player.Volume = MediaElement.ShouldMute ? 0 : volumeBeforeMute;
    }

    protected virtual partial void PlatformUpdateShouldLoopPlayback()
    {
        if (Player is null)
        {
            return;
        }

        Player.RepeatMode = MediaElement.ShouldLoopPlayback ? RepeatModeUtil.RepeatToggleModeOne : RepeatModeUtil.RepeatToggleModeNone;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            session?.Release();
            session?.Dispose();
            session = null;

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            if (connection is not null)
            {
                StopService(connection);
                connection.Dispose();
                connection = null;
            }

            AndroidArtworkBytesFetcher.DisposeClient();
            AndroidGlobalExoPlayerFactory.Reset();
        }
    }

    [MemberNotNull(nameof(connection))]
    void StartService()
    {
        var intent = new Intent(Application.Context, typeof(MediaControlsService));
        connection = new BoundServiceConnection(this);
        connection.MediaControlsServiceTaskRemoved += HandleMediaControlsServiceTaskRemoved;

        Application.Context.StartForegroundService(intent);
        Application.Context.ApplicationContext?.BindService(intent, connection, Bind.AutoCreate);
    }

    void StopService(in BoundServiceConnection boundServiceConnection)
    {
        boundServiceConnection.MediaControlsServiceTaskRemoved -= HandleMediaControlsServiceTaskRemoved;

        var serviceIntent = new Intent(Platform.AppContext, typeof(MediaControlsService));
        Application.Context.StopService(serviceIntent);
        Platform.AppContext.UnbindService(boundServiceConnection);
    }

    void HandleMediaControlsServiceTaskRemoved(object? sender, EventArgs e)
    {
        Player?.Stop();
    }

    public void OnAudioAttributesChanged(AudioAttributes? audioAttributes) { }
    public void OnAudioSessionIdChanged(int audioSessionId) { }
    public void OnAvailableCommandsChanged(PlayerCommands? player) { }
    public void OnCues(CueGroup? cues) { }
    public void OnDeviceInfoChanged(DeviceInfo? deviceInfo) { }
    public void OnDeviceVolumeChanged(int volume, bool muted) { }
    public void OnEvents(IPlayer? player, PlayerEvents? playerEvents) { }
    public void OnIsLoadingChanged(bool isLoading) { }
    public void OnIsPlayingChanged(bool isPlaying) { }
    public void OnLoadingChanged(bool isLoading) { }
    public void OnMaxSeekToPreviousPositionChanged(long maxSeekToPreviousPositionMs) { }
    public void OnMediaItemTransition(MediaItem? mediaItem, int reason) { }
    public void OnMediaMetadataChanged(MediaMetadata? mediaMetadata) { }
    public void OnMetadata(Metadata? metadata) { }
    public void OnPlayWhenReadyChanged(bool playWhenReady, int reason) { }
    public void OnPositionDiscontinuity(PlayerPositionInfo? oldPosition, PlayerPositionInfo? newPosition, int reason) { }
    public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason) { }
    public void OnPlayerErrorChanged(PlaybackException? error) { }
    public void OnPlaylistMetadataChanged(MediaMetadata? mediaMetadata) { }
    public void OnRenderedFirstFrame() { }
    public void OnRepeatModeChanged(int repeatMode) { }
    public void OnSeekBackIncrementChanged(long seekBackIncrementMs) { }
    public void OnSeekForwardIncrementChanged(long seekForwardIncrementMs) { }
    public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
    public void OnSkipSilenceEnabledChanged(bool skipSilenceEnabled) { }
    public void OnSurfaceSizeChanged(int width, int height) { }
    public void OnTimelineChanged(Timeline? timeline, int reason) { }
    public void OnTrackSelectionParametersChanged(TrackSelectionParameters? trackSelectionParameters) { }
    public void OnTracksChanged(Tracks? tracks) { }

}