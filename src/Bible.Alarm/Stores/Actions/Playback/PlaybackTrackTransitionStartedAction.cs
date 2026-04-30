namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Dispatched when the user presses Next or Previous and the app is fetching/preparing the new track.
/// Enables immediate progress bar animation before ExoPlayer reports Buffering.
/// </summary>
public sealed class PlaybackTrackTransitionStartedAction
{
    /// <summary>Version marker for non-empty Fluxor action type surface.</summary>
    internal const byte FluxorPayloadVersion = 1;
}
