namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Dispatched when track transition completes (playback started, failed, or user dismissed).
/// Clears the immediate progress indicator shown during Next/Previous.
/// </summary>
public sealed record PlaybackTrackTransitionEndedAction()
{
    /// <summary>Version marker for non-empty Fluxor action type surface.</summary>
    internal const byte FluxorPayloadVersion = 1;
}
