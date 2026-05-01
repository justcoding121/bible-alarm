#nullable enable

using CommunityToolkit;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Playback;
using Windows.System.Display;
using WindowsMediaElement = Windows.Media.Playback.MediaPlayer;

namespace CommunityToolkit.Maui.Views;

internal readonly record struct HeadlessMediaPlayerCallbacks(
    TypedEventHandler<WindowsMediaElement, object> MediaOpened,
    TypedEventHandler<WindowsMediaElement, MediaPlayerFailedEventArgs> MediaFailed,
    TypedEventHandler<WindowsMediaElement, object> MediaEnded,
    TypedEventHandler<WindowsMediaElement, object> VolumeChanged,
    TypedEventHandler<WindowsMediaElement, object> IsMutedChanged);

internal readonly record struct HeadlessPlaybackSessionCallbacks(
    TypedEventHandler<MediaPlaybackSession, object> NaturalVideoSizeChanged,
    TypedEventHandler<MediaPlaybackSession, object> PlaybackRateChanged,
    TypedEventHandler<MediaPlaybackSession, object> PlaybackStateChanged,
    TypedEventHandler<MediaPlaybackSession, object> SeekCompleted);

internal static class WindowsMediaManagerHeadlessInitializer
{
    internal static (WindowsMediaElement MediaPlayer, SystemMediaTransportControls SystemMediaControls) CreateHeadlessMediaPlayer(
        HeadlessMediaPlayerCallbacks playerCallbacks,
        HeadlessPlaybackSessionCallbacks sessionCallbacks)
    {
        WindowsMediaElement mediaPlayer = new();
        mediaPlayer.MediaOpened += playerCallbacks.MediaOpened;
        mediaPlayer.MediaFailed += playerCallbacks.MediaFailed;
        mediaPlayer.MediaEnded += playerCallbacks.MediaEnded;
        mediaPlayer.VolumeChanged += playerCallbacks.VolumeChanged;
        mediaPlayer.IsMutedChanged += playerCallbacks.IsMutedChanged;

        // Set up system media transport controls for headless mode
        // Enable SMTC to show native Windows media controls (taskbar, lock screen, volume flyout)
        var systemMediaControls = mediaPlayer.SystemMediaTransportControls;
        systemMediaControls.IsEnabled = true;
        systemMediaControls.IsPlayEnabled = true;
        systemMediaControls.IsPauseEnabled = true;
        systemMediaControls.IsNextEnabled = true;
        systemMediaControls.IsPreviousEnabled = true;
        // Enable fast forward and rewind (seek) controls
        systemMediaControls.IsFastForwardEnabled = true;
        systemMediaControls.IsRewindEnabled = true;
        systemMediaControls.PlaybackStatus = MediaPlaybackStatus.Stopped;

        // Set up event handlers for headless mode
        mediaPlayer.PlaybackSession.NaturalVideoSizeChanged += sessionCallbacks.NaturalVideoSizeChanged;
        mediaPlayer.PlaybackSession.PlaybackRateChanged += sessionCallbacks.PlaybackRateChanged;
        mediaPlayer.PlaybackSession.PlaybackStateChanged += sessionCallbacks.PlaybackStateChanged;
        mediaPlayer.PlaybackSession.SeekCompleted += sessionCallbacks.SeekCompleted;

        return (mediaPlayer, systemMediaControls);
    }
}

