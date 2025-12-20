using System;
using System.Text.Json.Serialization;

namespace Bible.Alarm.AudioLinksHarvestor.Models.Bible;

public class BibleChapter : IComparable
{
    public int Number { get; set; }
    public string Url { get; set; }

    [JsonIgnore]
    public string Title => $"Chapter {Number}";

    public int CompareTo(object obj) => Number.CompareTo((obj as BibleChapter).Number);
}
