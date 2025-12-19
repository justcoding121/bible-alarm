#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bible.Alarm.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Models.Schedule;

[Serializable]
[Table("AlarmMusic")]
[Index(nameof(AlarmScheduleId), IsUnique = true)]
[Index(nameof(PublicationCode), nameof(LanguageCode))]
public class AlarmMusic
{
    [Key]
    public int Id { get; set; }

    [Required]
    public MusicType MusicType { get; set; }

    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int TrackNumber { get; set; }

    //Always play current track.
    [Required]
    public bool Repeat { get; set; }

    [Required]
    [ForeignKey(nameof(AlarmSchedule))]
    public int AlarmScheduleId { get; set; }

    [Required]
    public virtual AlarmSchedule AlarmSchedule { get; set; } = null!;
}