using Fluxor;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class TrackSelectionAction
{
    public AlarmMusic TentativeMusic { get; }

    public TrackSelectionAction(AlarmMusic tentativeMusic)
    {
        TentativeMusic = tentativeMusic;
    }
}