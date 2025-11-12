using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class TrackSelectedAction(AlarmMusic currentMusic)
{
    public AlarmMusic CurrentMusic { get; } = currentMusic;
}