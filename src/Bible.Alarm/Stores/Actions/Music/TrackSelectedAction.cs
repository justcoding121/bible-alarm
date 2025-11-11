using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class TrackSelectedAction
{
    public AlarmMusic CurrentMusic { get; }

    public TrackSelectedAction(AlarmMusic currentMusic)
    {
        CurrentMusic = currentMusic;
    }
}