namespace Bible.Alarm.Models.Schedule;

[Serializable]
public class BibleReadingSchedule
{
    public int Id { get; set; }

    public string LanguageCode { get; set; } = string.Empty;
    public string PublicationCode { get; set; } = string.Empty;

    public int BookNumber { get; set; }
    public int ChapterNumber { get; set; }
    public TimeSpan FinishedDuration { get; set; }

    public virtual AlarmSchedule? AlarmSchedule { get; set; }
    public int AlarmScheduleId { get; set; }
}