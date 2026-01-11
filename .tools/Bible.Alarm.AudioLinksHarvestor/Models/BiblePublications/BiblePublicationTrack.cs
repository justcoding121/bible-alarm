using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications;

public class BiblePublicationTrack : IComparable
{
    public int Number { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public int CompareTo(object obj) => Number.CompareTo((obj as BiblePublicationTrack)?.Number ?? 0);
}
