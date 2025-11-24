namespace Bible.Alarm.Stores.Actions.Playback;

public class PlaybackStartedAction(int scheduleId)
{
    public int ScheduleId { get; } = scheduleId;
}

