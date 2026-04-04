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

    /// <summary>
    /// Called when the app resumes from background. If playback is active but neither
    /// the modal nor the mini bar is visible (e.g. playback started from Control Center
    /// or system media controls while backgrounded), shows the playback modal.
    /// </summary>
    Task ShowPlaybackModalIfNeededOnResumeAsync();

    /// <summary>
    /// Shows the mini playback bar immediately if playback is active and no playback UI
    /// is currently visible. Called synchronously on the main thread at the start of
    /// OnResume so the user sees instant feedback while the full modal is prepared.
    /// </summary>
    void ShowMiniBarIfPlaybackActiveOnResume();

    bool IsMinimized { get; }

    /// <summary>
    /// True when the playback modal is on screen OR a show request is pending (modal about to open).
    /// Used to defer home-list reordering until the modal covers the list.
    /// </summary>
    bool IsModalOpenOrPending { get; }

    bool WasRecentlyMinimized();
}
