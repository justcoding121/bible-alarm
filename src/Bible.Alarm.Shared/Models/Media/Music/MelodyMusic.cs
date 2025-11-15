using System.Collections.Generic;

namespace Bible.Alarm.Shared.Models.Media.Music
{
    public class MelodyMusic : Publication
    {
        public int Id { get; set; }
        public List<MusicTrack> Tracks { get; set; } = [];
    }
}
