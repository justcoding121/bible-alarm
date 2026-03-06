namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Dispatched when the user presses Next or Previous and the app is fetching/preparing the new track.
/// Enables immediate progress bar animation before ExoPlayer reports Buffering.
/// </summary>
public class PlaybackTrackTransitionStartedAction
{
}
