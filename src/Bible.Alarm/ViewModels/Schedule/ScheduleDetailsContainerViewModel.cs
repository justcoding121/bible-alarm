using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class ScheduleDetailsContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    private TimeSpan time;
    private DaysOfWeek daysOfWeek;
    private string name = string.Empty;
    private bool isEnabled;

    public ScheduleDetailsContainerViewModel(
        ILogger logger, 
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        
        state.StateChanged += OnStateChanged;
        InitializeCommands();
        InitializeFromState();
    }

    private void InitializeCommands()
    {
        ToggleDayCommand = new RelayCommand<object>(ToggleDay);
    }

    private void InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            time = new TimeSpan(currentSchedule.Hour, currentSchedule.Minute, currentSchedule.Second);
            daysOfWeek = currentSchedule.DaysOfWeek;
            name = currentSchedule.Name;
            isEnabled = currentSchedule.IsEnabled;

            OnPropertyChanged(nameof(Time));
            OnPropertyChanged(nameof(DaysOfWeek));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        InitializeFromState();
    }

    public ICommand ToggleDayCommand { get; private set; } = null!;

    public TimeSpan Time
    {
        get => time;
        set
        {
            if (SetProperty(ref time, value))
            {
                DispatchScheduleUpdate(s => 
                {
                    s.Hour = value.Hours;
                    s.Minute = value.Minutes;
                    s.Second = value.Seconds;
                });
            }
        }
    }

    public DaysOfWeek DaysOfWeek
    {
        get => daysOfWeek;
        set
        {
            if (SetProperty(ref daysOfWeek, value))
            {
                DispatchScheduleUpdate(s => s.DaysOfWeek = value);
            }
        }
    }

    public string Name
    {
        get => name;
        set
        {
            logger.Debug("Name: Setting value from '{OldValue}' to '{NewValue}'", name, value);
            if (SetProperty(ref name, value))
            {
                DispatchScheduleUpdate(s => s.Name = value);
            }
        }
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (SetProperty(ref isEnabled, value))
            {
                DispatchScheduleUpdate(s => s.IsEnabled = value);
            }
        }
    }

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        // Clone the current schedule and apply the update
        var updatedSchedule = CloneScheduleStateItem(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false));
    }

    private static ScheduleStateItem CloneScheduleStateItem(ScheduleStateItem source)
    {
        return new ScheduleStateItem
        {
            Id = source.Id,
            Name = source.Name,
            IsEnabled = source.IsEnabled,
            Hour = source.Hour,
            Minute = source.Minute,
            Second = source.Second,
            DaysOfWeek = source.DaysOfWeek,
            NotificationEnabled = source.NotificationEnabled,
            MusicEnabled = source.MusicEnabled,
            SnoozeMinutes = source.SnoozeMinutes,
            NumberOfChaptersToRead = source.NumberOfChaptersToRead,
            AlwaysPlayFromStart = source.AlwaysPlayFromStart,
            CurrentPlayItem = source.CurrentPlayItem,
            LatestAlarmNotificationId = source.LatestAlarmNotificationId,
            BibleReadingScheduleId = source.BibleReadingScheduleId,
            BibleReadingLanguageCode = source.BibleReadingLanguageCode,
            BibleReadingPublicationCode = source.BibleReadingPublicationCode,
            BibleReadingBookNumber = source.BibleReadingBookNumber,
            BibleReadingChapterNumber = source.BibleReadingChapterNumber,
            BibleReadingFinishedDuration = source.BibleReadingFinishedDuration,
            MusicId = source.MusicId,
            MusicType = source.MusicType,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackNumber = source.MusicTrackNumber,
            MusicRepeat = source.MusicRepeat,
            BibleReadingLanguageName = source.BibleReadingLanguageName,
            BibleReadingBookName = source.BibleReadingBookName
        };
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

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

