using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class SongBookSelectionAction(AlarmMusic tentativeMusic)
{
    public AlarmMusic TentativeMusic { get; } = tentativeMusic;
}