using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class SongBookSelectionAction
{
    public AlarmMusic TentativeMusic { get; }

    public SongBookSelectionAction(AlarmMusic tentativeMusic)
    {
        TentativeMusic = tentativeMusic;
    }
}