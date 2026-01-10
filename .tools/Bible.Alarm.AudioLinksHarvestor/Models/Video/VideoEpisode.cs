#nullable enable

using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models.Video;

public class VideoEpisode : IComparable
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LookUpPath { get; set; } = string.Empty;
    public double Duration { get; set; }

    public int CompareTo(object? obj) => Number.CompareTo((obj as VideoEpisode)?.Number ?? 0);
}
