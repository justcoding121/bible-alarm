#nullable enable

using Bible.Alarm.ViewModels;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages UI-bound properties for HomeViewModel.
/// </summary>
public class PropertyManager
{
    private ObservableHashSet<ScheduleListItemViewModel> schedules = [];
    private bool isBusy = true;
    private bool loaded;
    private ScheduleViewModel? selectedSchedule;

    public ObservableHashSet<ScheduleListItemViewModel> Schedules
    {
        get => schedules;
        set
        {
            if (schedules != value)
            {
                schedules = value;
                SchedulesChanged?.Invoke();
            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (isBusy != value)
            {
                isBusy = value;
                Loaded = !isBusy;
                IsBusyChanged?.Invoke(value);
            }
        }
    }

    public bool Loaded
    {
        get => loaded;
        set => loaded = value;
    }

    public ScheduleViewModel? SelectedSchedule
    {
        get => selectedSchedule;
        set => selectedSchedule = value;
    }

    public bool IsHomePageOverlayVisible => false;

    public event Action? SchedulesChanged;
    public event Action<bool>? IsBusyChanged;
}

