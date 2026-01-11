#nullable enable
namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Android-specific service for enabling the Next and Previous buttons in system media controls
/// by creating a multi-item queue in ExoPlayer.
/// </summary>
public interface IAndroidPlayerNotificationService : IDisposable
{
    /// <summary>
    /// Sets a multi-item queue via ExoPlayer to enable the Next and Previous buttons in system controls.
    /// Uses SetMediaSources with dummy items to create a proper multi-item timeline.
    /// </summary>
    void SetSourceWithDummyQueue(MediaElement mediaElement, string uri, bool isFirstTrack = false, bool isLastTrack = false);

    /// <summary>
    /// Releases the MediaSession to hide the media notification.
    /// Should be called when playback stops.
    /// </summary>
    Task ReleaseMediaSessionAsync(MediaElement mediaElement);
}

