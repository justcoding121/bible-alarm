#nullable enable
namespace Bible.Alarm.Services.Media.Interfaces;

/// <summary>
/// Android-specific service for enabling the Next button in system media controls
/// by creating a multi-item queue in ExoPlayer.
/// </summary>
public interface IAndroidNextButtonService
{
    /// <summary>
    /// Sets a multi-item queue via ExoPlayer to enable the Next button in system controls.
    /// Uses ConcatenatingMediaSource with a dummy item to create a proper multi-item timeline.
    /// </summary>
    /// <param name="uri">The URI of the current track</param>
    void SetSourceWithDummyQueue(string uri);
}

