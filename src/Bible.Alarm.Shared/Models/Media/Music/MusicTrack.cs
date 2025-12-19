#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media.Music;

[Table("MusicTrack")]
public class MusicTrack : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Number { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public AudioSource? Source { get; set; }

    public int CompareTo(object obj)
    {
        if (obj is not MusicTrack other)
        {
            return 1;
        }

        return Number.CompareTo(other.Number);
    }
}
