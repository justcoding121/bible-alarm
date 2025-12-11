using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Models;
using Fluxor;

namespace Bible.Alarm.Stores;

[FeatureState]
public class ApplicationState
{
    public ObservableHashSet<ScheduleStateItem> Schedules { get; set; }

    public ScheduleStateItem? CurrentSchedule { get; set; }

    public MusicStateItem? CurrentMusic { get; set; }
    public MusicStateItem? TentativeMusic { get; set; }

    public BibleReadingStateItem? CurrentBibleReadingSchedule { get; set; }
    public BibleReadingStateItem? TentativeBibleReadingSchedule { get; set; }

    public bool IsHomePageOverlayVisible { get; set; }
    public bool IsSchedulePageOverlayVisible { get; set; }

    public ApplicationState()
    {
        Schedules = [];
        IsHomePageOverlayVisible = false;
        IsSchedulePageOverlayVisible = false;
    }

    public ApplicationState(
        ObservableHashSet<ScheduleStateItem> schedules,
        ScheduleStateItem? currentSchedule = null,
        MusicStateItem? currentMusic = null,
        MusicStateItem? tentativeMusic = null,
        BibleReadingStateItem? currentBibleReadingSchedule = null,
        BibleReadingStateItem? tentativeBibleReadingSchedule = null,
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