using System;
using System.Collections.Generic;

namespace Bible.Alarm.Models
{
    public class BibleBook : IComparable
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public int Number { get; set; }

        public int BibleTranslationId { get; set; }
        public BibleTranslation BibleTranslation { get; set; }

        public List<BibleChapter> Chapters { get; set; } = new List<BibleChapter>();

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleBook).Number);
        }
    }
}
