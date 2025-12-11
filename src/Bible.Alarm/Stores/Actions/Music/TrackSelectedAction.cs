using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Music;

public class TrackSelectedAction(MusicStateItem currentMusic)
{
    public MusicStateItem CurrentMusic { get; } = currentMusic;
}