using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Serialization;

namespace Bible.Alarm.Models.Schedule;

[Serializable]
[Table("BibleReadingSchedules")]
[Index(nameof(AlarmScheduleId), IsUnique = true)]
public class BibleReadingSchedule
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    [Required]
    public int BookNumber { get; set; }

    [Required]
    public int ChapterNumber { get; set; }

    [Required]
    public TimeSpan FinishedDuration { get; set; }

    [Required]
    [ForeignKey(nameof(AlarmSchedule))]
    public int AlarmScheduleId { get; set; }

    [Required]
    public virtual AlarmSchedule AlarmSchedule { get; set; } = null!;
}