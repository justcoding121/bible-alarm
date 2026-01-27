#nullable enable

using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages UI-bound properties for HomeViewModel.
/// </summary>
public class PropertyManager
{
    private ObservableHashSet<ScheduleListItemViewModel> schedules = [];
    private bool isBusy = true;
    private bool loaded;
    private bool isAddBusy;

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
        set
        {
            if (loaded != value)
            {
                loaded = value;
                LoadedChanged?.Invoke(value);
            }
        }
    }

    public bool IsAddBusy
    {
        get => isAddBusy;
        set
        {
            if (isAddBusy != value)
            {
                isAddBusy = value;
                IsAddBusyChanged?.Invoke(value);
            }
        }
    }

    public event Action? SchedulesChanged;
    public event Action<bool>? IsBusyChanged;
    public event Action<bool>? LoadedChanged;
    public event Action<bool>? IsAddBusyChanged;
}

