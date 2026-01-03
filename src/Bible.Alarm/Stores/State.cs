#nullable enable

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

    public BibleReadingStateItem? CurrentBibleReadingSchedule { get; set; }

    public bool IsHomePageOverlayVisible { get; set; }
    public bool IsSchedulePageOverlayVisible { get; set; }
    public ContainerReadiness ContainerReadiness { get; set; }

    public ApplicationState()
    {
        Schedules = [];
        IsHomePageOverlayVisible = false;
        IsSchedulePageOverlayVisible = false;
        ContainerReadiness = Models.ContainerReadiness.NotReady;
    }

    public ApplicationState(
        ObservableHashSet<ScheduleStateItem> schedules,
        ScheduleStateItem? currentSchedule = null,
        MusicStateItem? currentMusic = null,
        BibleReadingStateItem? currentBibleReadingSchedule = null,
        bool isHomePageOverlayVisible = false,
        bool isSchedulePageOverlayVisible = false,
        ContainerReadiness? containerReadiness = null)
    {
        Schedules = schedules ?? [];
        CurrentSchedule = currentSchedule;
        CurrentMusic = currentMusic;
        CurrentBibleReadingSchedule = currentBibleReadingSchedule;
        IsHomePageOverlayVisible = isHomePageOverlayVisible;
        IsSchedulePageOverlayVisible = isSchedulePageOverlayVisible;
        ContainerReadiness = containerReadiness ?? Models.ContainerReadiness.NotReady;
    }
}
