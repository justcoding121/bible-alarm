namespace Bible.Alarm.Services.UI.Interfaces;

public interface IPlaybackModalService : IDisposable
{
    void SubscribeToPlaybackStateChanges();
    void UnsubscribeToPlaybackStateChanges();

    /// <summary>
    /// Called after window/UI is created (e.g., cold/warm start foregrounding).
    /// Shows PlaybackModal ONLY if playback is already active (Playing/Paused/Loading).
    /// Never shows for Failed/Stopped/Ended in this entrypoint.
    /// Returns true if the modal was shown.
    /// </summary>
    Task<bool> ShowPlaybackModalIfNeededOnWindowCreationAsync();

    bool IsMinimized { get; }

    /// <summary>
    /// True when the playback modal is on screen OR a show request is pending (modal about to open).
    /// Used to defer home-list reordering until the modal covers the list.
    /// </summary>
    bool IsModalOpenOrPending { get; }

    bool WasRecentlyMinimized();
}
