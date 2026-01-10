using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models.Bible;

public class BibleSection : IComparable
{
    public string Name { get; set; }
    public int Number { get; set; }

    public int CompareTo(object obj) => Number.CompareTo((obj as BibleSection).Number);
}
