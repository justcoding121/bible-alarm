using System.Windows.Input;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class ScheduleDetailsContainerViewModel : ObservableObject
{
    private readonly ILogger logger;

    private AlarmSchedule? model;
    private TimeSpan time;
    private DaysOfWeek daysOfWeek;
    private string name = string.Empty;
    private bool isEnabled;

    public ScheduleDetailsContainerViewModel(ILogger logger)
    {
        this.logger = logger;
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        ToggleDayCommand = new RelayCommand<object>(ToggleDay);
    }

    public void Initialize(AlarmSchedule model, TimeSpan time, DaysOfWeek daysOfWeek, string name, bool isEnabled)
    {
        this.model = model;
        this.time = time;
        this.daysOfWeek = daysOfWeek;
        this.name = name;
        this.isEnabled = isEnabled;

        OnPropertyChanged(nameof(Time));
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsEnabled));
    }

    public ICommand ToggleDayCommand { get; private set; } = null!;

    public TimeSpan Time
    {
        get => time;
        set
        {
            if (SetProperty(ref time, value) && model != null)
            {
                model.Hour = value.Hours;
                model.Minute = value.Minutes;
            }
        }
    }

    public DaysOfWeek DaysOfWeek
    {
        get => daysOfWeek;
        set
        {
            if (SetProperty(ref daysOfWeek, value) && model != null)
            {
                model.DaysOfWeek = value;
            }
        }
    }

    public string Name
    {
        get => name;
        set
        {
            logger.Debug("Name: Setting value from '{OldValue}' to '{NewValue}'", name, value);
            if (SetProperty(ref name, value) && model != null)
            {
                model.Name = value;
            }
        }
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (SetProperty(ref isEnabled, value) && model != null)
            {
                model.IsEnabled = value;
            }
        }
    }

    private void ToggleDay(object? parameter)
    {
        DaysOfWeek day;

        if (parameter is DaysOfWeek dayEnum)
        {
            day = dayEnum;
        }
        else if (parameter is string dayString && Enum.TryParse<DaysOfWeek>(dayString, out var parsedDay))
        {
            day = parsedDay;
        }
        else
        {
            // Invalid parameter
            return;
        }

        if ((DaysOfWeek & day) == day)
        {
            DaysOfWeek &= ~day;
        }
        else
        {
            DaysOfWeek |= day;
        }

        OnPropertyChanged(nameof(DaysOfWeek));
    }
}

