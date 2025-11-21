using System;
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Models.Media
{
    public class TrackMetadata
    {
        public long ScheduleId { get; set; }
        public DateTimeOffset NotificationTime { get; set; }

        public PlayType PlayType => BookNumber > 0 ? PlayType.Bible : PlayType.Music;

        public string LanguageCode { get; set; }
        public string PublicationCode { get; set; }

        public string LookUpPath { get; set; }

        public int BookNumber { get; set; }
        public int ChapterNumber { get; set; }

        public int TrackNumber { get; set; }

        public TimeSpan FinishedDuration { get; set; }

        public bool IsAlarmMusic => TrackNumber > 0;
        public bool IsBibleReading => ChapterNumber > 0;

        public bool IsLastTrack { get; set; }
    }
}
