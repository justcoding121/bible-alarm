#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Schedule;

[Serializable]
[Table("AlarmMusic")]
[Index(nameof(AlarmScheduleId), IsUnique = true)]
[Index(nameof(PublicationCode), nameof(LanguageCode))]
public class AlarmMusic
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    /// <summary>
    /// Section code for music publications with sections (e.g., "iam" Kingdom Melodies discs).
    /// Null for music publications without sections.
    /// Matches BiblePublicationSection.SectionCode for consistency.
    /// </summary>
    [MaxLength(50)]
    public string? SectionCode { get; set; }

    /// <summary>
    /// Track code from API (e.g. "1", "2" for track number within publication or disc).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TrackCode { get; set; } = string.Empty;

    //Always play current track.
    [Required]
    public bool Repeat { get; set; }

    [Required]
    [ForeignKey(nameof(AlarmSchedule))]
    public int AlarmScheduleId { get; set; }

    [Required]
    public virtual AlarmSchedule AlarmSchedule { get; set; } = null!;
}
