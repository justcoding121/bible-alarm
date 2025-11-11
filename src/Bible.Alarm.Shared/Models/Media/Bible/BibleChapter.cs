using System;

namespace Bible.Alarm.Shared.Models.Media.Bible
{
    public class BibleChapter : IComparable
    {
        public int Id { get; set; }

        public int Number { get; set; }

        public string Title => $"Chapter {Number}";

        public AudioSource? Source { get; set; }

        public int BibleBookId { get; set; }
        public BibleBook? Book { get; set; }

        public int CompareTo(object? obj)
        {
            if (obj is not BibleChapter other) return 1;
            return Number.CompareTo(other.Number);
        }
    }
}
