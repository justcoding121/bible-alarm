#nullable enable

using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models.Drama;

public class DramaTrack : IComparable
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LookUpPath { get; set; } = string.Empty;

    public int CompareTo(object? obj) => Number.CompareTo((obj as DramaTrack)?.Number ?? 0);
}
