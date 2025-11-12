using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Models.Media.Bible
{
    public class BibleBook : IComparable
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public int Number { get; set; }

        public int BibleTranslationId { get; set; }
        public BibleTranslation BibleTranslation { get; set; }

        public List<BibleChapter> Chapters { get; set; } = new List<BibleChapter>();

        public int CompareTo(object obj)
        {
            if (obj is not BibleBook other) return 1;
            return Number.CompareTo(other.Number);
        }
    }
}
