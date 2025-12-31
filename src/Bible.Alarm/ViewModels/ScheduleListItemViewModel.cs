#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
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
    private bool isInitializing;
    private AlarmSchedule? lastKnownSchedule;
    private string? lastKnownBibleReadingLanguageName;
    private string? lastKnownBookName;
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
        if (schedule == null || schedule.Id <= 0)
        {
            logger.Warning("InitializeFromSchedule: Invalid schedule or schedule ID {ScheduleId}", schedule?.Id ?? 0);
            return;
        }

        // Get schedule state item if not provided (for subtitle tracking)
        scheduleStateItem ??= applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == schedule.Id);

        // Use shared initialization method
        InitializeCommon(schedule, scheduleStateItem);
    }

    /// <summary>
    /// Initializes the ScheduleListItem from state using the schedule ID.
    /// This follows the state-driven architecture pattern where view models initialize from state.
    /// </summary>
    public void SetScheduleId(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            logger.Warning("SetScheduleId: Invalid schedule ID {ScheduleId}", scheduleId);
            return;
        }

        // Find schedule from state
        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            logger.Warning("SetScheduleId: Schedule {ScheduleId} not found in state", scheduleId);
            return;
        }

        // Map ScheduleStateItem to AlarmSchedule entity
        var schedule = mapper.Map<AlarmSchedule>(scheduleStateItem);

        // Use shared initialization method
        InitializeCommon(schedule, scheduleStateItem);
    }

    /// <summary>
    /// Common initialization logic shared by InitializeFromSchedule and SetScheduleId.
    /// Sets up the schedule, property notifications, event subscriptions, and commands.
    /// </summary>
    private void InitializeCommon(AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem)
    {
        isInitializing = true;
        try
        {
            Schedule = schedule;
            isEnabled = schedule.IsEnabled;
        }
        finally
        {
            isInitializing = false;
        }

        // Trigger property change notifications (UI thread operation)
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(Hour));
        OnPropertyChanged(nameof(Minute));
        OnPropertyChanged(nameof(Meridian));
        OnPropertyChanged(nameof(MeridianText));
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(MusicEnabled));
        // Note: SubTitle and Language will be set by RefreshSubTitleFromState() below

        // Subscribe to ApplicationState changes to react when this schedule is updated
        applicationState.StateChanged += OnApplicationStateChanged;
        // Store initial state for comparison
        lastKnownSchedule = Schedule;

        // Initialize tracked subtitle values from state
        if (scheduleStateItem != null)
        {
            lastKnownBibleReadingLanguageName = scheduleStateItem.BibleReadingLanguageName;
            lastKnownBookName = scheduleStateItem.BibleReadingBookName;
        }

        // Subscribe to PlaybackState changes to manage IsBusy
        playbackState.StateChanged += OnPlaybackStateChanged;

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

        // Initialize subtitle and language from state (BookName is pre-populated during bootstrap)
        // Use the provided scheduleStateItem if available to avoid re-looking it up
        RefreshSubTitleFromState(scheduleStateItem);

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(Schedule.Id))
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToPreviousBibleChapter(Schedule.Id);
                });
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(Schedule.Id))
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToNextBibleChapter(Schedule.Id);
                });
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
        });

        DeleteCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule == null || Schedule.Id <= 0)
            {
                return;
            }

            // Check if this is the last schedule - prevent deletion if it is
            var scheduleCount = applicationState.Value.Schedules?.Count ?? 0;
            if (scheduleCount <= 1)
            {
                logger.Warning("Cannot delete schedule {ScheduleId} - it is the last schedule", Schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));
                return;
            }

            // Dispatch DeleteScheduleAction (following Fluxor best practices)
            // The Effect will handle the actual DB deletion and dispatch success/failure actions
            logger.Information("ScheduleListItem: Dispatching DeleteScheduleAction for ScheduleId={ScheduleId}", Schedule.Id);
            dispatcher.Dispatch(new DeleteScheduleAction(Schedule.Id));
        });
    }

    public int ScheduleId => Schedule?.Id ?? 0;

    public string Name => Schedule?.Name ?? string.Empty;

    public string SubTitle { get; private set; } = string.Empty;

    public string Language { get; private set; } = string.Empty;

    public bool MusicEnabled => Schedule?.MusicEnabled ?? false;

    private bool isEnabled;

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (SetProperty(ref isEnabled, value) && !isInitializing && Schedule != null)
            {
                _ = HandleIsEnabledChanged(value);
            }
        }
    }

    private async Task HandleIsEnabledChanged(bool newValue)
    {
        try
        {
            NotifyThisPropertyChanged();
            var success = await scheduleStateService.UpdateScheduleEnabledStateAsync(ScheduleId, newValue);

            if (!success)
            {
                await RevertIsEnabledChange(newValue);
            }
            else
            {
                NotifyPropertiesChanged();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while handling IsEnabled change for schedule {ScheduleId}", ScheduleId);
            await RevertIsEnabledChange(newValue);
        }
    }

    private void NotifyThisPropertyChanged()
    {
        OnPropertyChanged(nameof(This));
    }

    private async Task RevertIsEnabledChange(bool attemptedValue)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            isEnabled = !attemptedValue;
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

        var scheduleId = Schedule.Id;

        try
        {
            // Use provided scheduleStateItem if available, otherwise look it up from state
            var scheduleStateItem = providedScheduleStateItem ?? 
                applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);

            if (scheduleStateItem?.BibleReadingScheduleId.HasValue == true)
            {
                UpdateLanguageFromState(scheduleStateItem);
                var subtitle = BuildSubtitleFromState(scheduleStateItem);
                if (!string.IsNullOrEmpty(subtitle))
                {
                    SubTitle = subtitle;
                    OnPropertyChanged(nameof(SubTitle));
                    return;
                }
            }
            else
            {
                ClearLanguage();
            }

            _ = RefreshChapterNameAsync(force: false);
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing subtitle from state for schedule {ScheduleId}", scheduleId);
            _ = RefreshChapterNameAsync(force: false);
        }
    }

    private void UpdateLanguageFromState(ScheduleStateItem scheduleStateItem)
    {
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageName))
        {
            Language = scheduleStateItem.BibleReadingLanguageName;
        }
        else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode))
        {
            Language = scheduleStateItem.BibleReadingLanguageCode;
        }
        else
        {
            Language = string.Empty;
        }
        OnPropertyChanged(nameof(Language));
    }

    private static string BuildSubtitleFromState(ScheduleStateItem scheduleStateItem)
    {
        var subtitleParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingBookName))
        {
            subtitleParts.Add(scheduleStateItem.BibleReadingBookName);
        }
        else if (scheduleStateItem.BibleReadingBookNumber.HasValue && scheduleStateItem.BibleReadingBookNumber.Value > 0)
        {
            subtitleParts.Add($"Book {scheduleStateItem.BibleReadingBookNumber.Value}");
        }

        if (scheduleStateItem.BibleReadingChapterNumber.HasValue && scheduleStateItem.BibleReadingChapterNumber.Value > 0)
        {
            subtitleParts.Add(scheduleStateItem.BibleReadingChapterNumber.Value.ToString());
        }

        return subtitleParts.Count > 0 ? string.Join(" ", subtitleParts) : string.Empty;
    }

    private void ClearLanguage()
    {
        Language = string.Empty;
        OnPropertyChanged(nameof(Language));
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
        // Capture to avoid null reference
        var schedule = Schedule;
        if (schedule == null)
        {
            return;
        }

        var scheduleId = schedule.Id;

        try
        {
            // Try to get Language from state first (synchronous)
            var scheduleStateItem = applicationState.Value.Schedules
                .FirstOrDefault(s => s.Id == scheduleId);

            string language = string.Empty;
            if (scheduleStateItem != null)
            {
                if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageName))
                {
                    language = scheduleStateItem.BibleReadingLanguageName;
                }
                else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode))
                {
                    language = scheduleStateItem.BibleReadingLanguageCode;
                }
            }

            // Run database operations off UI thread
            var displayName = await Task.Run(async () =>
                await displayService.GetChapterDisplayNameAsync(scheduleId, force));

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    SubTitle = displayName;
                    OnPropertyChanged(nameof(SubTitle));
                }

                // Update Language property
                Language = language;
                OnPropertyChanged(nameof(Language));
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing chapter name for schedule {ScheduleId}", scheduleId);
        }
    }

    public void RefreshChapterName(bool force = false) =>
        // Try state first, then fallback to async lookup
        RefreshSubTitleFromState();

    public int CompareTo(object? obj) => obj is not ScheduleListItemViewModel other ? 1 : ScheduleId.CompareTo(other.ScheduleId);

    private void OnApplicationStateChanged(object? sender, EventArgs e)
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

        var scheduleId = schedule.Id;
        var updatedScheduleItem = applicationState.Value.Schedules
            .FirstOrDefault(s => s.Id == scheduleId);

        if (updatedScheduleItem == null)
        {
            return;
        }

        var changeInfo = DetectScheduleChanges(updatedScheduleItem);
        UpdateScheduleFromState(updatedScheduleItem, changeInfo);
        NotifyPropertyChanges(changeInfo);
    }

    private ScheduleChangeInfo DetectScheduleChanges(ScheduleStateItem updatedScheduleItem)
    {
        var oldBookNumber = Schedule?.BibleReadingSchedule?.BookNumber;
        var oldChapterNumber = Schedule?.BibleReadingSchedule?.ChapterNumber;
        var oldDaysOfWeek = Schedule?.DaysOfWeek ?? 0;
        var oldIsEnabled = isEnabled;
        var oldName = Schedule?.Name ?? string.Empty;
        var oldHour = Schedule?.Hour ?? 0;
        var oldMinute = Schedule?.Minute ?? 0;
        var oldMusicEnabled = Schedule?.MusicEnabled ?? false;

        var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
        var trackChanged = DetectTrackChange(updatedSchedule);
        var newBibleReadingLanguageName = updatedScheduleItem.BibleReadingLanguageName;
        var newBookName = updatedScheduleItem.BibleReadingBookName;

        return new ScheduleChangeInfo
        {
            UpdatedSchedule = updatedSchedule,
            TrackChanged = trackChanged,
            BookNumberChanged = oldBookNumber != updatedSchedule.BibleReadingSchedule?.BookNumber,
            ChapterNumberChanged = oldChapterNumber != updatedSchedule.BibleReadingSchedule?.ChapterNumber,
            BibleReadingLanguageNameChanged = lastKnownBibleReadingLanguageName != newBibleReadingLanguageName,
            BookNameChanged = lastKnownBookName != newBookName,
            DaysOfWeekChanged = oldDaysOfWeek != updatedSchedule.DaysOfWeek,
            IsEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled,
            NameChanged = oldName != updatedSchedule.Name,
            TimeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute,
            MusicEnabledChanged = oldMusicEnabled != updatedSchedule.MusicEnabled,
            NewBibleReadingLanguageName = newBibleReadingLanguageName,
            NewBookName = newBookName
        };
    }

    private bool DetectTrackChange(AlarmSchedule updatedSchedule)
    {
        if (lastKnownSchedule?.BibleReadingSchedule != null && updatedSchedule.BibleReadingSchedule != null)
        {
            return lastKnownSchedule.BibleReadingSchedule.BookNumber != updatedSchedule.BibleReadingSchedule.BookNumber ||
                   lastKnownSchedule.BibleReadingSchedule.ChapterNumber != updatedSchedule.BibleReadingSchedule.ChapterNumber;
        }

        if (lastKnownSchedule?.Music != null && updatedSchedule.Music != null)
        {
            return lastKnownSchedule.Music.TrackNumber != updatedSchedule.Music.TrackNumber;
        }

        return false;
    }

    private void UpdateScheduleFromState(ScheduleStateItem updatedScheduleItem, ScheduleChangeInfo changeInfo)
    {
        Schedule = changeInfo.UpdatedSchedule;
        lastKnownSchedule = changeInfo.UpdatedSchedule;
        isEnabled = changeInfo.UpdatedSchedule.IsEnabled;

        var subtitleChanged = changeInfo.TrackChanged || changeInfo.BookNumberChanged || changeInfo.ChapterNumberChanged ||
                             changeInfo.BibleReadingLanguageNameChanged || changeInfo.BookNameChanged;

        if (subtitleChanged)
        {
            lastKnownBibleReadingLanguageName = changeInfo.NewBibleReadingLanguageName;
            lastKnownBookName = changeInfo.NewBookName;
            // Use the updated scheduleStateItem to ensure we have the latest data
            RefreshSubTitleFromState(updatedScheduleItem);
        }
    }

    private void NotifyPropertyChanges(ScheduleChangeInfo changeInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(DaysOfWeek));
            OnPropertyChanged(nameof(This));

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

    private record ScheduleChangeInfo
    {
        public AlarmSchedule UpdatedSchedule { get; init; } = null!;
        public bool TrackChanged { get; init; }
        public bool BookNumberChanged { get; init; }
        public bool ChapterNumberChanged { get; init; }
        public bool BibleReadingLanguageNameChanged { get; init; }
        public bool BookNameChanged { get; init; }
        public bool DaysOfWeekChanged { get; init; }
        public bool IsEnabledChanged { get; init; }
        public bool NameChanged { get; init; }
        public bool TimeChanged { get; init; }
        public bool MusicEnabledChanged { get; init; }
        public string? NewBibleReadingLanguageName { get; init; }
        public string? NewBookName { get; init; }
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
