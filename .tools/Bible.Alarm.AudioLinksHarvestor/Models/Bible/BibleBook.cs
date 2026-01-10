using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models.Bible;

public class BiblePublicationSection : IComparable
{
    public string Name { get; set; }
    public int Number { get; set; }

    public int CompareTo(object obj) => Number.CompareTo((obj as BiblePublicationSection).Number);
}
