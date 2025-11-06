using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.ViewModels.Redux;

public class ApplicationState
{
    public ObservableHashSet<AlarmSchedule> Schedules { get; set; }

    public AlarmSchedule CurrentSchedule { get; set; }

    public AlarmMusic CurrentMusic { get; set; }
    public AlarmMusic TentativeMusic { get; set; }

    public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }
}