#nullable enable

using CommunityToolkit;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Playback;
using Windows.System.Display;
using WindowsMediaElement = Windows.Media.Playback.MediaPlayer;

namespace CommunityToolkit.Maui.Views;

internal static class WindowsMediaManagerHeadlessInitializer
{
    internal static (WindowsMediaElement MediaPlayer, SystemMediaTransportControls SystemMediaControls) CreateHeadlessMediaPlayer(
        TypedEventHandler<WindowsMediaElement, object> mediaOpened,
        TypedEventHandler<WindowsMediaElement, MediaPlayerFailedEventArgs> mediaFailed,
        TypedEventHandler<WindowsMediaElement, object> mediaEnded,
        TypedEventHandler<WindowsMediaElement, object> volumeChanged,
        TypedEventHandler<WindowsMediaElement, object> isMutedChanged,
        TypedEventHandler<MediaPlaybackSession, object> naturalVideoSizeChanged,
        TypedEventHandler<MediaPlaybackSession, object> playbackRateChanged,
        TypedEventHandler<MediaPlaybackSession, object> playbackStateChanged,
        TypedEventHandler<MediaPlaybackSession, object> seekCompleted)
    {
        WindowsMediaElement mediaPlayer = new();
        mediaPlayer.MediaOpened += mediaOpened;
        mediaPlayer.MediaFailed += mediaFailed;
        mediaPlayer.MediaEnded += mediaEnded;
        mediaPlayer.VolumeChanged += volumeChanged;
        mediaPlayer.IsMutedChanged += isMutedChanged;

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
        mediaPlayer.PlaybackSession.NaturalVideoSizeChanged += naturalVideoSizeChanged;
        mediaPlayer.PlaybackSession.PlaybackRateChanged += playbackRateChanged;
        mediaPlayer.PlaybackSession.PlaybackStateChanged += playbackStateChanged;
        mediaPlayer.PlaybackSession.SeekCompleted += seekCompleted;

        return (mediaPlayer, systemMediaControls);
    }
}

