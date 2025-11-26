namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Action dispatched when track duration changes (e.g., when a new track starts).
/// Duration is infrequent, so it stays in Fluxor state.
/// </summary>
public class PlaybackDurationChangedAction
{
    public TimeSpan Duration { get; init; }
}

