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

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Section number for traditional Bible readings (1-66).
    /// Null for drama publications which don't have sections.
    /// Use PublicationTypeHelper.HasSectionStructure() to check if this applies.
    /// </summary>
    [Range(1, 66)]
    public int? SectionNumber { get; set; }

    /// <summary>
    /// Track number for traditional Bible readings, or track/part number for dramas.
    /// </summary>
    [Required]
    [Range(1, 500)]
    public int TrackNumber { get; set; }

    [Required]
    public TimeSpan FinishedDuration { get; set; }

    [Required]
    [ForeignKey(nameof(AlarmSchedule))]
    public int AlarmScheduleId { get; set; }

    [Required]
    public virtual AlarmSchedule AlarmSchedule { get; set; } = null!;
}
