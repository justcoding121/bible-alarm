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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class ScheduleListItem(
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

    public void Initialize(AlarmSchedule schedule)
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

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(Hour));
        OnPropertyChanged(nameof(Minute));
        OnPropertyChanged(nameof(Meridian));
        OnPropertyChanged(nameof(MeridianText));
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(MusicEnabled));

        // Subscribe to ApplicationState changes to react when this schedule is updated
        applicationState.StateChanged += OnApplicationStateChanged;
        // Store initial state for comparison
        lastKnownSchedule = Schedule;

        // Initialize tracked subtitle values from state
        var initialStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == schedule.Id);
        lastKnownBibleReadingLanguageName = initialStateItem?.BibleReadingLanguageName;
        lastKnownBookName = initialStateItem?.BibleReadingBookName;

        // Subscribe to PlaybackState changes to manage IsBusy
        playbackState.StateChanged += OnPlaybackStateChanged;

        PlayCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0)
            {
                // Notify HomeViewModel to show overlay immediately
                onPlayStarted?.Invoke();
                // Wait 50ms to ensure overlay is visible before starting playback
                await Task.Delay(50);
                await playbackService.PlayScheduleAsync(Schedule.Id);
            }
        });

        // Initialize subtitle from state (BookName is pre-populated during bootstrap)
        RefreshSubTitleFromState();

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(Schedule.Id))
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToPreviousBibleChapter(Schedule.Id);
                });
                RefreshSubTitleFromState();
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
                RefreshSubTitleFromState();
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
            // Immediately notify UI that This property changed so day button colors update instantly
            OnPropertyChanged(nameof(This));

            var success = await scheduleStateService.UpdateScheduleEnabledStateAsync(ScheduleId, newValue);

            if (!success)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    isEnabled = !newValue;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(This));
                });
            }
            else
            {
                RaisePropertiesChangedEvent();
                // Ensure This is also notified for immediate UI update
                OnPropertyChanged(nameof(This));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while handling IsEnabled change for schedule {ScheduleId}", ScheduleId);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                isEnabled = !newValue;
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(This));
            });
        }
    }

    public DaysOfWeek DaysOfWeek => Schedule?.DaysOfWeek ?? 0;

    public string TimeText => Schedule?.TimeText ?? string.Empty;

    public string Hour => Schedule?.MeridianHour.ToString("D2") ?? "00";

    public string Minute => Schedule?.Minute.ToString("D2") ?? "00";

    public Meridian Meridian => Schedule?.Meridian ?? Meridian.Am;

    public string MeridianText => Meridian.ToString().ToUpperInvariant();

    public ScheduleListItem This => this;

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
    private void RefreshSubTitleFromState()
    {
        if (Schedule == null || Schedule.Id <= 0)
        {
            return;
        }

        var scheduleId = Schedule.Id;

        try
        {
            // Get ScheduleStateItem from state (BookName is pre-populated during bootstrap)
            var scheduleStateItem = applicationState.Value.Schedules?
                .FirstOrDefault(s => s.Id == scheduleId);

            if (scheduleStateItem?.BibleReadingScheduleId.HasValue == true)
            {
                // Set language separately (for display below switch)
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

                // Build subtitle from state (synchronous, no database call)
                // Format: "BookName ChapterNumber" (e.g., "Mark 1" instead of "Mark • Chapter 1")
                var subtitleParts = new List<string>();

                // Add book name (pre-populated during bootstrap) or fallback to book number
                if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingBookName))
                {
                    subtitleParts.Add(scheduleStateItem.BibleReadingBookName);
                }
                else if (scheduleStateItem.BibleReadingBookNumber.HasValue && scheduleStateItem.BibleReadingBookNumber.Value > 0)
                {
                    subtitleParts.Add($"Book {scheduleStateItem.BibleReadingBookNumber.Value}");
                }

                // Add chapter number (without "Chapter" prefix)
                if (scheduleStateItem.BibleReadingChapterNumber.HasValue && scheduleStateItem.BibleReadingChapterNumber.Value > 0)
                {
                    subtitleParts.Add(scheduleStateItem.BibleReadingChapterNumber.Value.ToString());
                }

                if (subtitleParts.Count > 0)
                {
                    SubTitle = string.Join(" ", subtitleParts); // Use space instead of bullet
                    OnPropertyChanged(nameof(SubTitle));
                    return;
                }
            }
            else
            {
                // No Bible reading schedule - clear language
                Language = string.Empty;
                OnPropertyChanged(nameof(Language));
            }

            // Fallback: If BookName not available in state, do async lookup (for backward compatibility)
            _ = RefreshChapterNameAsync(force: false);
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing subtitle from state for schedule {ScheduleId}", scheduleId);
            // Fallback to async lookup on error
            _ = RefreshChapterNameAsync(force: false);
        }
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

    public int CompareTo(object? obj) => obj is not ScheduleListItem other ? 1 : ScheduleId.CompareTo(other.ScheduleId);

    private void OnApplicationStateChanged(object? sender, EventArgs e)
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

        // Find the updated schedule in the state (ScheduleStateItem DTO)
        var updatedScheduleItem = applicationState.Value.Schedules
            .FirstOrDefault(s => s.Id == scheduleId);

        if (updatedScheduleItem == null)
        {
            return;
        }

        // Store old subtitle-related values BEFORE updating
        // Get current values from SubTitle property (which reflects what's currently displayed)
        // and from the current Schedule entity
        var oldBookNumber = Schedule?.BibleReadingSchedule?.BookNumber;
        var oldChapterNumber = Schedule?.BibleReadingSchedule?.ChapterNumber;

        // Map DTO to entity for comparison and storage
        var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);

        // Check if the track/chapter changed by comparing BibleReadingSchedule or Music properties
        var trackChanged = false;

        if (lastKnownSchedule?.BibleReadingSchedule != null && updatedSchedule.BibleReadingSchedule != null)
        {
            // Check if book or chapter changed
            if (lastKnownSchedule.BibleReadingSchedule.BookNumber != updatedSchedule.BibleReadingSchedule.BookNumber ||
                lastKnownSchedule.BibleReadingSchedule.ChapterNumber != updatedSchedule.BibleReadingSchedule.ChapterNumber)
            {
                trackChanged = true;
            }
        }
        else if (lastKnownSchedule?.Music != null && updatedSchedule.Music != null)
        {
            // Check if track number changed
            if (lastKnownSchedule.Music.TrackNumber != updatedSchedule.Music.TrackNumber)
            {
                trackChanged = true;
            }
        }

        // Store old values before updating
        var oldDaysOfWeek = Schedule?.DaysOfWeek ?? 0;
        var oldIsEnabled = isEnabled;
        var oldName = Schedule?.Name ?? string.Empty;
        var oldHour = Schedule?.Hour ?? 0;
        var oldMinute = Schedule?.Minute ?? 0;
        var oldMusicEnabled = Schedule?.MusicEnabled ?? false;

        // Update the schedule reference
        Schedule = updatedSchedule;
        lastKnownSchedule = updatedSchedule;

        // Check if properties changed by comparing old values with new values
        var daysOfWeekChanged = oldDaysOfWeek != updatedSchedule.DaysOfWeek;
        var isEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled;
        var nameChanged = oldName != updatedSchedule.Name;
        var timeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute;
        var musicEnabledChanged = oldMusicEnabled != updatedSchedule.MusicEnabled;

        // Check if subtitle-related properties changed
        var newBibleReadingLanguageName = updatedScheduleItem.BibleReadingLanguageName;
        var newBookName = updatedScheduleItem.BibleReadingBookName;
        var bookNumberChanged = oldBookNumber != updatedSchedule.BibleReadingSchedule?.BookNumber;
        var chapterNumberChanged = oldChapterNumber != updatedSchedule.BibleReadingSchedule?.ChapterNumber;
        var bibleReadingLanguageNameChanged = lastKnownBibleReadingLanguageName != newBibleReadingLanguageName;
        var bookNameChanged = lastKnownBookName != newBookName;

        // Only refresh subtitle if subtitle-related properties actually changed
        var subtitleChanged = trackChanged || bookNumberChanged || chapterNumberChanged ||
                             bibleReadingLanguageNameChanged || bookNameChanged;

        if (subtitleChanged)
        {
            // Update tracked values
            lastKnownBibleReadingLanguageName = newBibleReadingLanguageName;
            lastKnownBookName = newBookName;
            RefreshChapterName();
        }

        // Always update isEnabled to match the schedule
        isEnabled = updatedSchedule.IsEnabled;

        // Notify property changes for updated properties on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Always notify DaysOfWeek and This when schedule is updated to ensure UI refreshes
            // This is critical for day indicator updates on the home page
            OnPropertyChanged(nameof(DaysOfWeek));
            // Also notify This for day indicator bindings
            OnPropertyChanged(nameof(This));

            if (isEnabledChanged)
            {
                OnPropertyChanged(nameof(IsEnabled));
            }
            if (nameChanged)
            {
                OnPropertyChanged(nameof(Name));
            }
            if (timeChanged)
            {
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(Hour));
                OnPropertyChanged(nameof(Minute));
                OnPropertyChanged(nameof(Meridian));
                OnPropertyChanged(nameof(MeridianText));
            }
            if (musicEnabledChanged)
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
