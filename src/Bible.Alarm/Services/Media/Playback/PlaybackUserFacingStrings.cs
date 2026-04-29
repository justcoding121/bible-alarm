#nullable enable

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// User-visible playback strings (modal errors, toasts). Centralized for Sonar S1192 and consistency.
/// </summary>
internal static class PlaybackUserFacingStrings
{
    public const string PlaybackRetryConnectionThenTapRetry = "Playback failed, check your connection then tap Retry";
    public const string DownloadFailedCheckInternet = "Download failed, check your internet connection";
    public const string MediaDownloadFailedCheckInternet = "Media download failed, check your internet connection";
    public const string MediaPlaybackFailedPlayingFallbackAlarm = "Media playback failed. Playing fallback alarm sound.";
    public const string MediaPlaybackFailedCheckInternet = "Media playback failed. Check your internet connection.";
    public const string MediaPlaybackFailedPleaseTryAgainModal = "Media playback failed. Please try again.";
    public const string MediaPlaybackFailedPleaseTryAgainToast = "Media playback failed, please try again";
    public const string PlaybackFailedTapRetry = "Playback failed, tap Retry";
    public const string DownloadFailedPlayingDefaultAlarmSound = "Download failed, playing default alarm sound";
}
