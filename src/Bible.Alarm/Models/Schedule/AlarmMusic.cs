using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Models.Schedule;

[Serializable]
public class AlarmMusic
{
    public int Id { get; set; }

    public MusicType MusicType { get; set; }
    public string PublicationCode { get; set; } = string.Empty;

    public string? LanguageCode { get; set; }
    public int TrackNumber { get; set; }

    //Always play current track.
    public bool Repeat { get; set; }

    public virtual AlarmSchedule? AlarmSchedule { get; set; }
    public int AlarmScheduleId { get; set; }
}