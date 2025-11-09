using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class MusicSelectionAction
{
    public AlarmMusic CurrentMusic { get; }

    public MusicSelectionAction(AlarmMusic currentMusic)
    {
        CurrentMusic = currentMusic;
    }
}