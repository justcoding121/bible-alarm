using System;

namespace Bible.Alarm.Shared.Models.Media.Music
{
    public class MusicTrack : IComparable
    {
        public int Id { get; set; }

        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;

        public AudioSource? Source { get; set; }

        public int CompareTo(object? obj)
        {
            if (obj is not MusicTrack other) return 1;
            return Number.CompareTo(other.Number);
        }
    }
}
