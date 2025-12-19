using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Music;

public class SongBookSelectionAction(MusicStateItem tentativeMusic)
{
    public MusicStateItem TentativeMusic { get; } = tentativeMusic;
}
