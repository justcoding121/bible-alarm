using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class MusicSelectionAction(AlarmMusic currentMusic)
{
    public AlarmMusic CurrentMusic { get; } = currentMusic;
}