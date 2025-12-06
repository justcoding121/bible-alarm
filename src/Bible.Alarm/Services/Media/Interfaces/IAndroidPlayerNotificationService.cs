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
    /// <param name="mediaElement">The MediaElement instance to use</param>
    /// <param name="uri">The URI of the current track</param>
    /// <param name="isFirstTrack">True if this is the first track in the playlist (no previous button needed)</param>
    /// <param name="isLastTrack">True if this is the last track in the playlist (no next button needed)</param>
    void SetSourceWithDummyQueue(CommunityToolkit.Maui.Views.MediaElement mediaElement, string uri, bool isFirstTrack = false, bool isLastTrack = false);

    /// <summary>
    /// Releases the MediaSession to hide the media notification.
    /// Should be called when playback stops.
    /// </summary>
    /// <param name="mediaElement">The MediaElement instance to release</param>
    void ReleaseMediaSession(CommunityToolkit.Maui.Views.MediaElement mediaElement);
}

