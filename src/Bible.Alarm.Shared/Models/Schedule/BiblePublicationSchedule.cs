#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Schedule;

[Serializable]
[Table("BiblePublicationSchedules")]
[Index(nameof(AlarmScheduleId), IsUnique = true)]
[Index(nameof(PublicationCode), nameof(LanguageCode))]
public class BiblePublicationSchedule
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Section code for publications with sections (e.g., "1" for book 1, "gen" for Genesis, section code for dramas).
    /// Null for publications without sections (e.g., dramas, videos).
    /// Matches BiblePublicationSection.SectionCode for consistency.
    /// </summary>
    [MaxLength(50)]
    public string? SectionCode { get; set; }

    /// <summary>
    /// Track code from API: chapter number as string for Bible (e.g. "1", "2"), pub value for drama (e.g. "iacu"), track number as string for music/video.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TrackCode { get; set; } = string.Empty;

    [Required]
    public TimeSpan FinishedDuration { get; set; }

    [Required]
    [ForeignKey(nameof(AlarmSchedule))]
    public int AlarmScheduleId { get; set; }

    [Required]
    public virtual AlarmSchedule AlarmSchedule { get; set; } = null!;
}
