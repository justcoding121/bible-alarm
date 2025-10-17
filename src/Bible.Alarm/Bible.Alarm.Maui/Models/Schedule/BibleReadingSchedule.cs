
using System;

namespace Bible.Alarm.Models
{
    [Serializable]
    public class BibleReadingSchedule
    {
        public int Id { get; set; }

        public string LanguageCode { get; set; }
        public string PublicationCode { get; set; }

        public int BookNumber { get; set; }
        public int ChapterNumber { get; set; }
        public TimeSpan FinishedDuration { get; set; }

        public virtual AlarmSchedule AlarmSchedule { get; set; }
        public int AlarmScheduleId { get; set; }
    }
}
