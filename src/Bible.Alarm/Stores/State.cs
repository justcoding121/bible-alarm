using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.Stores;

public class ApplicationState
{
    public ObservableHashSet<AlarmSchedule> Schedules { get; set; }

    public AlarmSchedule CurrentSchedule { get; set; }

    public AlarmMusic CurrentMusic { get; set; }
    public AlarmMusic TentativeMusic { get; set; }

    public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
}