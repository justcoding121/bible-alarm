namespace Bible.Alarm.Services.UI.Interfaces;

public interface IPlaybackModalService : IDisposable
{
    void SubscribeToPlaybackStateChanges();
    void UnsubscribeToPlaybackStateChanges();

    /// <summary>
    /// Called after window/UI is created (e.g., cold/warm start foregrounding).
     /// Shows PlaybackModal ONLY if playback is already active (Playing/Paused/Loading).
    /// Never shows for Failed/Stopped/Ended in this entrypoint.
    /// </summary>
    Task ShowPlaybackModalIfNeededOnWindowCreationAsync();
}
