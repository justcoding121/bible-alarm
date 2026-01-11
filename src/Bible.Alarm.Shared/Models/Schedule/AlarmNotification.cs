using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Schedule;

[Serializable]
[Table("AlarmNotifications")]
[Index(nameof(AlarmScheduleId))]
[Index(nameof(ScheduledTime))]
[Index(nameof(Sent), nameof(Fired))]
public class AlarmNotification
{
    [Key]
    public long Id { get; set; }

    [Required]
    public DateTimeOffset ScheduledTime { get; set; }

    [Required]
    public bool Sent { get; set; }

    [Required]
    public bool Fired { get; set; }

    [Required]
    [ForeignKey(nameof(AlarmSchedule))]
    public int AlarmScheduleId { get; set; }

    [Required]
    public AlarmSchedule AlarmSchedule { get; set; } = null!;

    [Required]
    public bool CancellationRequested { get; set; }

    [Required]
    public bool Cancelled { get; set; }
}
