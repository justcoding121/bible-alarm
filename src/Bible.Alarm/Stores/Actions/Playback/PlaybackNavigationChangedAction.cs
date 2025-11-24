namespace Bible.Alarm.Stores.Actions.Playback;

public class PlaybackNavigationChangedAction(bool canPlayNext, bool canPlayPrevious)
{
    public bool CanPlayNext { get; } = canPlayNext;
    public bool CanPlayPrevious { get; } = canPlayPrevious;
}

