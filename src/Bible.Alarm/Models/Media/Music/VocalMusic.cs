using Bible.Alarm.Shared.Models;
using Bible.Alarm.Shared.Models.Music;

namespace Bible.Alarm.Models.Media.Music;

public class VocalMusic : TranslatedPublication
{
    public int Id { get; set; }
    public List<MusicTrack> Tracks { get; set; } = [];
}