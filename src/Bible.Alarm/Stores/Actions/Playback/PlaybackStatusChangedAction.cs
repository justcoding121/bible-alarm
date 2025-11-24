using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Stores.Actions.Playback;

public class PlaybackStatusChangedAction(PlayStatus status)
{
    public PlayStatus Status { get; } = status;
}

