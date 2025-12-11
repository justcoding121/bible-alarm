using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.Music;

public class TrackSelectionAction(MusicStateItem tentativeMusic)
{
    public MusicStateItem TentativeMusic { get; } = tentativeMusic;
}