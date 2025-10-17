using System;

namespace Bible.Alarm.Models
{
    public class MusicTrack : IComparable
    {
        public int Id { get; set; }

        public int Number { get; set; }
        public string Title { get; set; }

        public AudioSource Source { get; set; }

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as MusicTrack).Number);
        }
    }
}
