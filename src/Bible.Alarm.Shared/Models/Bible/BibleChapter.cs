using System;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Models.Bible;

public class BibleChapter : IComparable
{
    public int Id { get; set; }

    public int Number { get; set; }

    public string Title => $"Chapter {Number}";

    public AudioSource Source { get; set; }

    public int BibleBookId { get; set; }
    public BibleBook Book { get; set; }
    
    // Backward compatibility properties
    public string Url { get; set; }
    public string LookUpPath { get; set; }

    public int CompareTo(object obj)
    {
        return Number.CompareTo((obj as BibleChapter).Number);
    }
}