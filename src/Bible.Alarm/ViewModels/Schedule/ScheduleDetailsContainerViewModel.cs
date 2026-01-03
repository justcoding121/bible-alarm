#nullable enable

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
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Microsoft.Maui.ApplicationModel;

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
    private bool hasSignaledReady;
    private int scheduleId;

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
            scheduleId = currentSchedule.Id;
            time = new TimeSpan(currentSchedule.Hour, currentSchedule.Minute, currentSchedule.Second);
            daysOfWeek = currentSchedule.DaysOfWeek;
            name = currentSchedule.Name;
            isEnabled = currentSchedule.IsEnabled;

            // Batch property notifications to reduce UI thread work
            NotifyScheduleDetailsPropertiesChanged();
            
            // Signal that this container is ready (initialized from CurrentSchedule)
            SignalContainerReady();
        }
    }

    private void SignalContainerReady()
    {
        if (hasSignaledReady) return;
        hasSignaledReady = true;
        
        // Dispatch to state that this container is ready
        MainThread.BeginInvokeOnMainThread(() =>
        {
            dispatcher.Dispatch(new ContainerReadyAction("ScheduleDetails"));
        });
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;
        
        // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
        // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
        if (hasSignaledReady && !stateValue.ContainerReadiness.ScheduleDetails && currentSchedule != null)
        {
            logger.Debug("ScheduleDetailsContainerViewModel: ContainerReadiness reset to NotReady, resetting hasSignaledReady flag and re-initializing");
            hasSignaledReady = false;
            // Re-initialize and signal ready again
            InitializeFromState();
            return;
        }
        
        // If we don't have a scheduleId yet (initial state), initialize when CurrentSchedule is set
        // But only if we haven't already signaled ready (prevents infinite loop for new schedules with Id=0)
        if (scheduleId == 0 && currentSchedule != null && !hasSignaledReady)
        {
            InitializeFromState();
            return;
        }
        
        // Reset hasSignaledReady when schedule ID changes to a different positive ID (existing schedule opened)
        if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
        {
            hasSignaledReady = false;
            InitializeFromState();
            return;
        }
        
        // Only update properties if they changed (don't re-initialize)
        if (currentSchedule != null && !hasSignaledReady)
        {
            // Handle case where InitializeFromState hasn't been called yet
            InitializeFromState();
        }
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
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
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
            BibleReadingPublicationName = source.BibleReadingPublicationName,
            BibleReadingBookName = source.BibleReadingBookName,
            MusicLanguageName = source.MusicLanguageName,
            MusicPublicationName = source.MusicPublicationName,
            MusicTrackName = source.MusicTrackName
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

    /// <summary>
    /// Notifies all schedule details properties changed in a single batch.
    /// This reduces UI thread work compared to individual notifications.
    /// </summary>
    private void NotifyScheduleDetailsPropertiesChanged()
    {
        OnPropertyChanged(nameof(Time));
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsEnabled));
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

