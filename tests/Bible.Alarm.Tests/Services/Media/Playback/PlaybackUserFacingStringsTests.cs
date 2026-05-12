#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackUserFacingStringsTests
{
    [Fact]
    public void PlaybackUserFacingStrings_strings_are_non_empty_and_stable()
    {
        Assert.Equal(
            "Playback failed, check your connection then tap Retry",
            PlaybackUserFacingStrings.PlaybackRetryConnectionThenTapRetry);

        Assert.Equal(
            "Download failed, check your internet connection",
            PlaybackUserFacingStrings.DownloadFailedCheckInternet);

        Assert.Equal(
            "Media download failed, check your internet connection",
            PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet);

        Assert.Equal(
            "Media playback failed. Playing fallback alarm sound.",
            PlaybackUserFacingStrings.MediaPlaybackFailedPlayingFallbackAlarm);

        Assert.Equal(
            "Media playback failed. Check your internet connection.",
            PlaybackUserFacingStrings.MediaPlaybackFailedCheckInternet);

        Assert.Equal(
            "Media playback failed. Please try again.",
            PlaybackUserFacingStrings.MediaPlaybackFailedPleaseTryAgainModal);

        Assert.Equal(
            "Media playback failed, please try again",
            PlaybackUserFacingStrings.MediaPlaybackFailedPleaseTryAgainToast);

        Assert.Equal(
            "Playback failed, tap Retry",
            PlaybackUserFacingStrings.PlaybackFailedTapRetry);

        Assert.Equal(
            "Download failed, playing default alarm sound",
            PlaybackUserFacingStrings.DownloadFailedPlayingDefaultAlarmSound);
    }
}
