namespace Bible.Alarm.Stores.Actions.Playback;

public class PlaybackPositionChangedAction
{
    public TimeSpan? CurrentPosition { get; init; }
    public TimeSpan Duration { get; init; }
}

