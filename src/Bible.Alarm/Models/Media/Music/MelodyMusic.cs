using Bible.Alarm.Shared.Models;
using Bible.Alarm.Shared.Models.Music;

namespace Bible.Alarm.Models;

public class MelodyMusic : Publication
{
    public int Id { get; set; }
    public List<MusicTrack> Tracks { get; set; } = [];
}