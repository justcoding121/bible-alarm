using System;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Models.Music;

public class MusicTrack : IComparable
{
    public int Id { get; set; }

    public int Number { get; set; }
    public string Title { get; set; }

    public AudioSource Source { get; set; }
    
    // Backward compatibility properties
    public string Url { get; set; }
    public string LookUpPath { get; set; }

    public int CompareTo(object obj)
    {
        return Number.CompareTo((obj as MusicTrack).Number);
    }
}