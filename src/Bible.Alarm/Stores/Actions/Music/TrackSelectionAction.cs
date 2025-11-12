using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class TrackSelectionAction(AlarmMusic tentativeMusic)
{
    public AlarmMusic TentativeMusic { get; } = tentativeMusic;
}