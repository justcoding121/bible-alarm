using System;

namespace Bible.Alarm.Models
{
    [Serializable]
    public class AlarmMusic
    {
        public int Id { get; set; }

        public MusicType MusicType { get; set; }
        public string PublicationCode { get; set; }

        public string LanguageCode { get; set; }
        public int TrackNumber { get; set; }

        //Always play current track.
        public bool Repeat { get; set; }

        public virtual AlarmSchedule AlarmSchedule { get; set; }
        public int AlarmScheduleId { get; set; }
    }

}
