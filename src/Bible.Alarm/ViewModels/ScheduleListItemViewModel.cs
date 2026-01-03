#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class ScheduleListItemViewModel(
    ILogger logger,
    ISchedulePlaybackService playbackService,
    IScheduleDisplayService displayService,
    IScheduleStateService scheduleStateService,
    IPlaylistService playlistService,
    IState<ApplicationState> applicationState,
    IState<PlaybackState> playbackState,
    IDispatcher dispatcher,
    IMapper mapper)
    : ObservableObject, IComparable, IDisposable
{
    // Helper classes
    private readonly ScheduleListItemInitializer initializer = new(logger, mapper, applicationState);
    private readonly ScheduleListItemPropertyManager propertyManager = new(logger, scheduleStateService);
    private readonly ScheduleListItemCommandHandler commandHandler = new(logger, playbackService, playlistService, applicationState, dispatcher);
    private readonly ScheduleListItemStateHandler stateHandler = new(logger, mapper, applicationState);
    private readonly ScheduleListItemSubtitleManager subtitleManager = new(logger, displayService, applicationState);

    private bool isBusy;
    private Action? onPlayStarted;
    private Action? onPlaybackStarted;

    public AlarmSchedule? Schedule { get; private set; }

    /// <summary>
    /// Action to call when play button is pressed. Used to show overlay on Home page.
    /// </summary>
    public Action? OnPlayStarted
    {
        get => onPlayStarted;
        set => onPlayStarted = value;
    }

    /// <summary>
    /// Action to call when playback actually starts (modal is shown). Used to hide overlay on Home page.
    /// </summary>
    public Action? OnPlaybackStarted
    {
        get => onPlaybackStarted;
        set => onPlaybackStarted = value;
    }

    /// <summary>
    /// Initializes the ScheduleListItem from pre-mapped AlarmSchedule data.
    /// This is optimized for performance - mapping happens off UI thread, only UI operations here.
    /// </summary>
    public void InitializeFromSchedule(AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem = null)
    {
        var (validSchedule, stateItem) = initializer.InitializeFromSchedule(schedule, scheduleStateItem);
        if (validSchedule == null)
        {
            return;
        }

        InitializeCommon(validSchedule, stateItem);
    }

    /// <summary>
    /// Initializes the ScheduleListItem from state using the schedule ID.
    /// This follows the state-driven architecture pattern where view models initialize from state.
    /// </summary>
    public void SetScheduleId(int scheduleId)
    {
        var (schedule, scheduleStateItem) = initializer.SetScheduleId(scheduleId);
        if (schedule == null)
        {
            return;
        }

        InitializeCommon(schedule, scheduleStateItem);
    }

    /// <summary>
    /// Common initialization logic shared by InitializeFromSchedule and SetScheduleId.
    /// Sets up the schedule, property notifications, event subscriptions, and commands.
    /// </summary>
    private void InitializeCommon(AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem)
    {
        propertyManager.IsInitializing = true;
        try
        {
            Schedule = schedule;
            var (isEnabled, _, _, _, _, _, _, _) = propertyManager.GetPropertiesFromSchedule(schedule);
            propertyManager.IsEnabled = isEnabled;
        }
        finally
        {
            propertyManager.IsInitializing = false;
        }

        // Trigger property change notifications (UI thread operation)
        // Ensure these are on UI thread for proper binding updates
        // NOTE: Also notify 'This' property to trigger converters that bind to the entire ViewModel
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(Hour));
            OnPropertyChanged(nameof(Minute));
            OnPropertyChanged(nameof(Meridian));
            OnPropertyChanged(nameof(MeridianText));
            OnPropertyChanged(nameof(DaysOfWeek));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(MusicEnabled));
            // Notify 'This' to trigger converters that bind to the entire ViewModel (e.g., dayColorConverter, dayBackgroundColorConverter)
            OnPropertyChanged(nameof(This));
            logger.Debug("ScheduleListItemViewModel: InitializeCommon - Notified all properties including This. ScheduleId={ScheduleId}, DaysOfWeek={DaysOfWeek}", 
                Schedule?.Id ?? 0, Schedule?.DaysOfWeek ?? 0);
        });
        // Note: SubTitle and Language will be set by RefreshSubTitleFromState() below

        // Subscribe to ApplicationState changes to react when this schedule is updated
        applicationState.StateChanged += OnApplicationStateChanged;
        // Store initial state for comparison
        stateHandler.LastKnownSchedule = Schedule;

        // Initialize tracked subtitle values from state
        if (scheduleStateItem != null)
        {
            stateHandler.LastKnownBibleReadingLanguageName = scheduleStateItem.BibleReadingLanguageName;
            stateHandler.LastKnownBookName = scheduleStateItem.BibleReadingBookName;
        }

        // Subscribe to PlaybackState changes to manage IsBusy
        playbackState.StateChanged += OnPlaybackStateChanged;

        // Initialize commands using helper (recreate with updated schedule)
        PlayCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0)
            {
                // Set IsBusy immediately to show loading indicator
                IsBusy = true;
                // Notify HomeViewModel to show overlay immediately
                onPlayStarted?.Invoke();
                // Wait 50ms to ensure overlay is visible before starting playback
                await Task.Delay(50);
                await playbackService.PlayScheduleAsync(Schedule.Id);
            }
        });
        // Recreate commands with updated schedule to ensure they use the latest chapter information
        PreviousCommand = commandHandler.CreatePreviousCommand(Schedule);
        NextCommand = commandHandler.CreateNextCommand(Schedule);
        DeleteCommand = commandHandler.CreateDeleteCommand(Schedule);

        // Initialize subtitle and language from state (BookName is pre-populated during bootstrap)
        // Use the provided scheduleStateItem if available to avoid re-looking it up
        RefreshSubTitleFromState(scheduleStateItem);
    }

    public int ScheduleId => Schedule?.Id ?? 0;

    public string Name => Schedule?.Name ?? string.Empty;

    public string SubTitle
    {
        get => subtitleManager.SubTitle;
        private set => subtitleManager.SubTitle = value;
    }

    public string Language
    {
        get => subtitleManager.Language;
        private set => subtitleManager.Language = value;
    }

    public bool MusicEnabled => Schedule?.MusicEnabled ?? false;

    public bool IsEnabled
    {
        get => propertyManager.IsEnabled;
        set
        {
            if (propertyManager.IsEnabled != value)
            {
                propertyManager.IsEnabled = value;
                OnPropertyChanged();
                if (!propertyManager.IsInitializing && Schedule != null)
                {
                    _ = HandleIsEnabledChanged(value);
                }
            }
        }
    }

    private async Task HandleIsEnabledChanged(bool newValue)
    {
        await propertyManager.HandleIsEnabledChanged(
            ScheduleId,
            newValue,
            Schedule,
            () => OnPropertyChanged(nameof(This)),
            () => NotifyPropertiesChanged(),
            async (attemptedValue) => await RevertIsEnabledChange(attemptedValue));
    }

    private void NotifyThisPropertyChanged()
    {
        OnPropertyChanged(nameof(This));
    }

    private async Task RevertIsEnabledChange(bool attemptedValue)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            propertyManager.IsEnabled = !attemptedValue;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(This));
        });
    }

    private void NotifyPropertiesChanged()
    {
        RaisePropertiesChangedEvent();
        OnPropertyChanged(nameof(This));
    }

    public DaysOfWeek DaysOfWeek => Schedule?.DaysOfWeek ?? 0;

    public string TimeText => Schedule?.TimeText ?? string.Empty;

    public string Hour => Schedule?.MeridianHour.ToString("D2") ?? "00";

    public string Minute => Schedule?.Minute.ToString("D2") ?? "00";

    public Meridian Meridian => Schedule?.Meridian ?? Meridian.Am;

    public string MeridianText => Meridian.ToString().ToUpperInvariant();

    public ScheduleListItemViewModel This => this;

    public ICommand PlayCommand { get; private set; } = null!;

    public ICommand PreviousCommand { get; set; } = null!;
    public ICommand NextCommand { get; set; } = null!;
    public ICommand DeleteCommand { get; private set; } = null!;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public void RaisePropertiesChangedEvent()
    {
        var properties = GetType()
            .GetProperties()
            .Where(x => x.Name != "IsEnabled")
            .Select(x => x.Name);
        foreach (var property in properties)
        {
            OnPropertyChanged(property);
        }
    }

    /// <summary>
    /// Refreshes subtitle from ScheduleStateItem in state (uses pre-populated BookName).
    /// Falls back to async database lookup if BookName is not available in state.
    /// </summary>
    private void RefreshSubTitleFromState(ScheduleStateItem? providedScheduleStateItem = null)
    {
        if (Schedule == null || Schedule.Id <= 0)
        {
            return;
        }

        subtitleManager.RefreshSubTitleFromState(
            Schedule.Id,
            providedScheduleStateItem,
            value => SubTitle = value,
            value => Language = value,
            OnPropertyChanged);
    }

    /// <summary>
    /// Async fallback method for refreshing chapter name from database.
    /// Only used if BookName is not available in state.
    /// </summary>
    public async Task RefreshChapterNameAsync(bool force = false)
    {
        if (Schedule?.Id <= 0)
        {
            return;
        }

        var schedule = Schedule;
        if (schedule == null)
        {
            return;
        }

        await subtitleManager.RefreshChapterNameAsync(
            schedule.Id,
            force,
            value => SubTitle = value,
            value => Language = value,
            OnPropertyChanged);
    }

    public void RefreshChapterName(bool force = false) =>
        // Try state first, then fallback to async lookup
        RefreshSubTitleFromState();

    public int CompareTo(object? obj) => obj is not ScheduleListItemViewModel other ? 1 : ScheduleId.CompareTo(other.ScheduleId);

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        var schedule = Schedule;
        if (schedule?.Id <= 0)
        {
            return;
        }

        if (schedule == null)
        {
            return;
        }

        var changeInfo = stateHandler.HandleApplicationStateChanged(schedule.Id, schedule);
        if (changeInfo == null)
        {
            return;
        }

        // Store old DaysOfWeek before updating to ensure we can detect changes
        var oldDaysOfWeek = schedule.DaysOfWeek;

        UpdateScheduleFromState(changeInfo);
        NotifyPropertyChanges(changeInfo);

        // Double-check DaysOfWeek change after update (in case comparison missed it)
        // This handles edge cases where the schedule was already updated but DaysOfWeek changed
        var updatedSchedule = changeInfo.UpdatedSchedule;
        if (!changeInfo.DaysOfWeekChanged && updatedSchedule != null && updatedSchedule.DaysOfWeek != oldDaysOfWeek)
        {
            logger.Debug("ScheduleListItemViewModel: DaysOfWeek changed but not detected by comparison. Old: {OldDaysOfWeek}, New: {NewDaysOfWeek}. Forcing property change.",
                oldDaysOfWeek, updatedSchedule.DaysOfWeek);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(DaysOfWeek));
                // Also notify 'This' to trigger converters that bind to the entire ViewModel
                OnPropertyChanged(nameof(This));
            });
        }
        else if (changeInfo.DaysOfWeekChanged)
        {
            logger.Debug("ScheduleListItemViewModel: DaysOfWeek change was detected. Old: {OldDaysOfWeek}, New: {NewDaysOfWeek}.",
                oldDaysOfWeek, Schedule?.DaysOfWeek ?? 0);
        }
    }

    private void UpdateScheduleFromState(ScheduleListItemStateHandler.ScheduleChangeInfo changeInfo)
    {
        var updatedSchedule = changeInfo.UpdatedSchedule;
        Schedule = updatedSchedule;
        stateHandler.LastKnownSchedule = updatedSchedule;
        propertyManager.IsEnabled = updatedSchedule.IsEnabled;

        var subtitleChanged = changeInfo.TrackChanged || changeInfo.BookNumberChanged || changeInfo.ChapterNumberChanged ||
                             changeInfo.BibleReadingLanguageNameChanged || changeInfo.BookNameChanged;

        if (subtitleChanged)
        {
            stateHandler.LastKnownBibleReadingLanguageName = changeInfo.NewBibleReadingLanguageName;
            stateHandler.LastKnownBookName = changeInfo.NewBookName;
            // Refresh subtitle from state
            var updatedScheduleItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == updatedSchedule.Id);
            RefreshSubTitleFromState(updatedScheduleItem);
        }
    }

    private void NotifyPropertyChanges(ScheduleListItemStateHandler.ScheduleChangeInfo changeInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Always notify 'This' first to trigger converters that bind to the entire ViewModel
            OnPropertyChanged(nameof(This));

            if (changeInfo.DaysOfWeekChanged)
            {
                logger.Debug("ScheduleListItemViewModel: NotifyPropertyChanges - DaysOfWeek changed for schedule {ScheduleId}. New value: {NewDaysOfWeek}",
                    ScheduleId, Schedule?.DaysOfWeek ?? 0);
                OnPropertyChanged(nameof(DaysOfWeek));
            }
            if (changeInfo.IsEnabledChanged)
            {
                OnPropertyChanged(nameof(IsEnabled));
            }
            if (changeInfo.NameChanged)
            {
                OnPropertyChanged(nameof(Name));
            }
            if (changeInfo.TimeChanged)
            {
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(Hour));
                OnPropertyChanged(nameof(Minute));
                OnPropertyChanged(nameof(Meridian));
                OnPropertyChanged(nameof(MeridianText));
            }
            if (changeInfo.MusicEnabledChanged)
            {
                OnPropertyChanged(nameof(MusicEnabled));
            }
        });
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        // Note: IsBusy is set to false by ScheduleItemStateService after the modal is actually shown.
        // This handler is kept for potential future use but doesn't hide the overlay anymore.
        // The overlay is hidden by AlarmModalService -> ScheduleItemStateService after modal is shown.
    }

    public void Dispose()
    {
        applicationState.StateChanged -= OnApplicationStateChanged;
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }
}
