#nullable enable

using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models.Music;

public class MusicTrack : IComparable
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    // LookUpPath is no longer stored in the database - it's computed at runtime
    // Keeping the property for backward compatibility with JSON deserialization
    public string LookUpPath { get; set; } = string.Empty;

    public int CompareTo(object? obj) => Number.CompareTo((obj as MusicTrack)?.Number ?? 0);
}
