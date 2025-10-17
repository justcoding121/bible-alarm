using System;

namespace Bible.Alarm.Models.Schedule
{
    [Serializable]
    public class AlarmNotification
    {
        public long Id { get; set; }

        public DateTimeOffset ScheduledTime { get; set; }
        public bool Sent { get; set; }
        public bool Fired { get; set; }
        public int AlarmScheduleId { get; set; }
        public AlarmSchedule AlarmSchedule { get; set; }
        public bool CancellationRequested { get; set; }
        public bool Cancelled { get; set; }
    }
}
