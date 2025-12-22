using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Music;

public class SongBookSelectionAction(MusicStateItem currentMusic)
{
    public MusicStateItem CurrentMusic { get; } = currentMusic;
}
