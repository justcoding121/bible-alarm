using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Models.Bible;

public class BibleBook : IComparable
{
    public string Name { get; set; }
    public int Number { get; set; }
    public List<BibleChapter> Chapters { get; set; } = new();
    public BibleTranslation BibleTranslation { get; set; }

    public int CompareTo(object obj)
    {
        return Number.CompareTo((obj as BibleBook).Number);
    }
}