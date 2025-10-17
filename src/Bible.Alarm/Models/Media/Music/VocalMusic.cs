using System.Collections.Generic;

namespace Bible.Alarm.Models
{
    public class VocalMusic : TranslatedPublication
    {
        public int Id { get; set; }
        public List<MusicTrack> Tracks { get; set; } = new List<MusicTrack>();
    }
}
