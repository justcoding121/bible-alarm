using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Music;

public class MusicSelectionAction(MusicStateItem currentMusic)
{
    public MusicStateItem CurrentMusic { get; } = currentMusic;
}