using System;
using System.Text.Json.Serialization;

namespace Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications;

public class BiblePublicationTrack : IComparable
{
    public int Number { get; set; }
    public string Url { get; set; }

    [JsonIgnore]
    public string Title => $"Track {Number}";

    public int CompareTo(object obj) => Number.CompareTo((obj as BiblePublicationTrack).Number);
}
