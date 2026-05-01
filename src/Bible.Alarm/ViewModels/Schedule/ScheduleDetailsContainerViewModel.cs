#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed partial class ScheduleDetailsContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IScheduleValidationService scheduleValidationService;

    private TimeSpan time;
    private WeekDays daysOfWeek;
    private string name = string.Empty;
    private bool isEnabled;
    private bool hasSignaledReady;
    private bool isReadyActionQueued;
    private bool isProcessingStateChange;
    private int scheduleId;
    private int themeVersion;

    public ScheduleDetailsContainerViewModel(
        ILogger logger,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        IScheduleValidationService scheduleValidationService)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.scheduleValidationService = scheduleValidationService;

        state.StateChanged += OnStateChanged;
        // Subscribe to theme changes to update day button colors
        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (r, m) => OnThemeChanged());
        InitializeCommands();
        InitializeFromState();
    }

    private void InitializeCommands()
    {
        ToggleDayCommand = new AsyncRelayCommand<object>(ToggleDayAsync);
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
        // Check if already signaled or already marked ready in state
        // This check must happen first to prevent any duplicate work
        if (hasSignaledReady || state.Value.ContainerReadiness.ScheduleDetails) return;

        // Check if action is already queued to prevent duplicate queued actions
        // This prevents multiple rapid calls from queuing multiple actions
        if (isReadyActionQueued) return;

        // Atomically set both flags to prevent race conditions
        // If another thread/call checks between these lines, it will see isReadyActionQueued=true
        isReadyActionQueued = true;
        hasSignaledReady = true;

        // Double-check state immediately after setting flags (before queuing)
        // This catches the case where state changed between the initial check and flag setting
        if (state.Value.ContainerReadiness.ScheduleDetails)
        {
            // State already shows ready, reset flags and return
            isReadyActionQueued = false;
            hasSignaledReady = true;
            return;
        }

        // Dispatch to state that this container is ready
        // Check state again inside the queued action to prevent duplicates from queued actions
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Reset flag when action executes
            isReadyActionQueued = false;

            // Final check before dispatching - if state already shows we're ready, another action already handled it
            if (state.Value.ContainerReadiness.ScheduleDetails)
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("ScheduleDetails"));
        });
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;

            if (TryHandleContainerReadinessReset(currentSchedule, stateValue))
            {
                return;
            }

            if (TryInitializeWhenScheduleAppears(currentSchedule))
            {
                return;
            }

            if (TryReinitializeWhenScheduleIdChanges(currentSchedule))
            {
                return;
            }

            if (currentSchedule != null)
            {
                SyncFromCurrentSchedule(currentSchedule);
            }
        }
        finally
        {
            isProcessingStateChange = false;
        }
    }

    private bool TryHandleContainerReadinessReset(ScheduleStateItem? currentSchedule, ApplicationState stateValue)
    {
        if (!(hasSignaledReady && !stateValue.ContainerReadiness.ScheduleDetails && currentSchedule != null))
        {
            return false;
        }

        hasSignaledReady = false;
        isReadyActionQueued = false;
        InitializeFromState();
        return true;
    }

    private bool TryInitializeWhenScheduleAppears(ScheduleStateItem? currentSchedule)
    {
        if (!(scheduleId == 0 && currentSchedule != null && !hasSignaledReady))
        {
            return false;
        }

        InitializeFromState();
        return true;
    }

    private bool TryReinitializeWhenScheduleIdChanges(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null || currentSchedule.Id == scheduleId || currentSchedule.Id <= 0)
        {
            return false;
        }

        hasSignaledReady = false;
        isReadyActionQueued = false;
        InitializeFromState();
        return true;
    }

    private void SyncFromCurrentSchedule(ScheduleStateItem currentSchedule)
    {
        if (!hasSignaledReady)
        {
            InitializeFromState();
            return;
        }

        SyncScheduleFieldsAfterInitialization(currentSchedule);
    }

    private void SyncScheduleFieldsAfterInitialization(ScheduleStateItem currentSchedule)
    {
        if (isEnabled != currentSchedule.IsEnabled)
        {
            isEnabled = currentSchedule.IsEnabled;
            OnPropertyChanged(nameof(IsEnabled));
        }

        var scheduleTime = new TimeSpan(currentSchedule.Hour, currentSchedule.Minute, currentSchedule.Second);
        if (time != scheduleTime)
        {
            time = scheduleTime;
            OnPropertyChanged(nameof(Time));
        }

        if (currentSchedule.DaysOfWeek == 0 && daysOfWeek != 0)
        {
            logger.Warning("ScheduleDetailsContainerViewModel: State has DaysOfWeek=0 but local has {LocalDaysOfWeek}. Preserving local value and fixing state.",
                daysOfWeek);
            DispatchScheduleUpdate(s => s.DaysOfWeek = daysOfWeek);
        }
        else if (daysOfWeek != currentSchedule.DaysOfWeek)
        {
            daysOfWeek = currentSchedule.DaysOfWeek;
            OnPropertyChanged(nameof(DaysOfWeek));
        }

        if (name != currentSchedule.Name)
        {
            name = currentSchedule.Name;
            OnPropertyChanged(nameof(Name));
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

    public WeekDays DaysOfWeek
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

    public int ThemeVersion => themeVersion;

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        var updatedSchedule = mapper.Map<ScheduleStateItem>(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    private async Task ToggleDayAsync(object? parameter)
    {
        WeekDays day;

        if (parameter is WeekDays dayEnum)
        {
            day = dayEnum;
        }
        else if (parameter is string dayString && Enum.TryParse<WeekDays>(dayString, out var parsedDay))
        {
            day = parsedDay;
        }
        else
        {
            // Invalid parameter
            return;
        }

        // Check if we're deselecting a day
        if ((DaysOfWeek & day) == day)
        {
            // Calculate what the new value would be
            var newDaysOfWeek = DaysOfWeek & ~day;

            // Count how many days are currently selected
            var currentCount = CountSelectedDays(DaysOfWeek);

            // If only one day is selected and we're trying to deselect it, prevent the change
            if (currentCount == 1)
            {
                logger.Debug("ToggleDay: Attempted to deselect the last remaining day");
                await scheduleValidationService.ValidateDaysOfWeekAsync(newDaysOfWeek);
                return;
            }

            DaysOfWeek = newDaysOfWeek;
        }
        else
        {
            DaysOfWeek = DaysOfWeek | day;
        }
    }

    private static int CountSelectedDays(WeekDays daysOfWeek)
    {
        var count = 0;
        foreach (WeekDays day in Enum.GetValues<WeekDays>())
        {
            if (day == WeekDays.All)
            {
                continue;
            }

            if ((daysOfWeek & day) == day)
            {
                count++;
            }
        }

        return count;
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

    private void OnThemeChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            themeVersion++;
            OnPropertyChanged(nameof(ThemeVersion));
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
    }
}

