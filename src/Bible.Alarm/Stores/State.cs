using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;
using Fluxor;

namespace Bible.Alarm.Stores;

[FeatureState]
public class ApplicationState
{
    public ObservableHashSet<AlarmSchedule> Schedules { get; set; }

    public AlarmSchedule CurrentSchedule { get; set; }

    public AlarmMusic CurrentMusic { get; set; }
    public AlarmMusic TentativeMusic { get; set; }

    public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
    public BibleReadingSchedule TentativeBibleReadingSchedule { get; set; }

    public bool IsHomePageOverlayVisible { get; set; }
    public bool IsSchedulePageOverlayVisible { get; set; }

    public ApplicationState()
    {
        Schedules = [];
        IsHomePageOverlayVisible = false;
        IsSchedulePageOverlayVisible = false;
    }

    public ApplicationState(
        ObservableHashSet<AlarmSchedule> schedules,
        AlarmSchedule currentSchedule,
        AlarmMusic currentMusic,
        AlarmMusic tentativeMusic,
        BibleReadingSchedule currentBibleReadingSchedule,
        BibleReadingSchedule tentativeBibleReadingSchedule,
        bool isHomePageOverlayVisible = false,
        bool isSchedulePageOverlayVisible = false)
    {
        Schedules = schedules ?? [];
        CurrentSchedule = currentSchedule;
        CurrentMusic = currentMusic;
        TentativeMusic = tentativeMusic;
        CurrentBibleReadingSchedule = currentBibleReadingSchedule;
        TentativeBibleReadingSchedule = tentativeBibleReadingSchedule;
        IsHomePageOverlayVisible = isHomePageOverlayVisible;
        IsSchedulePageOverlayVisible = isSchedulePageOverlayVisible;
    }
}