#nullable enable
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.ViewModels;

public class ScheduleListItem(
    ILogger logger,
    ISchedulePlaybackService playbackService,
    IScheduleDisplayService displayService,
    IScheduleStateService scheduleStateService,
    IPlaylistService playlistService,
    ISchedulePersistenceService schedulePersistenceService,
    IState<ApplicationState> applicationState,
    IState<PlaybackState> playbackState)
    : ObservableObject, IComparable, IDisposable
{
    private bool _isInitializing;
    private AlarmSchedule? _lastKnownSchedule;
    private bool _isBusy;
    private Action? _onPlayStarted;
    private Action? _onPlaybackStarted;

    public AlarmSchedule? Schedule { get; private set; }

    /// <summary>
    /// Action to call when play button is pressed. Used to show overlay on Home page.
    /// </summary>
    public Action? OnPlayStarted
    {
        get => _onPlayStarted;
        set => _onPlayStarted = value;
    }

    /// <summary>
    /// Action to call when playback actually starts (modal is shown). Used to hide overlay on Home page.
    /// </summary>
    public Action? OnPlaybackStarted
    {
        get => _onPlaybackStarted;
        set => _onPlaybackStarted = value;
    }

    public void Initialize(AlarmSchedule schedule)
    {
        _isInitializing = true;
        try
        {
            Schedule = schedule;
            _isEnabled = schedule.IsEnabled;
        }
        finally
        {
            _isInitializing = false;
        }

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(Hour));
        OnPropertyChanged(nameof(Minute));
        OnPropertyChanged(nameof(Meridian));
        OnPropertyChanged(nameof(MeridianText));
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(IsEnabled));

        // Subscribe to ApplicationState changes to react when this schedule is updated
        applicationState.StateChanged += OnApplicationStateChanged;
        // Store initial state for comparison
        _lastKnownSchedule = Schedule;

        // Subscribe to PlaybackState changes to manage IsBusy
        playbackState.StateChanged += OnPlaybackStateChanged;

        PlayCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0)
            {
                // Notify HomeViewModel to show overlay immediately
                _onPlayStarted?.Invoke();
                // Wait 50ms to ensure overlay is visible before starting playback
                await Task.Delay(50);
                await playbackService.PlayScheduleAsync(Schedule.Id);
            }
        });

        _ = RefreshChapterNameAsync(true);

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(Schedule.Id))
            {
                await playlistService.MoveToPreviousBibleChapter(Schedule.Id);
                await RefreshChapterNameAsync(true);
            }
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(Schedule.Id))
            {
                await playlistService.MoveToNextBibleChapter(Schedule.Id);
                await RefreshChapterNameAsync(true);
            }
        });

        DeleteCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0)
            {
                await schedulePersistenceService.DeleteScheduleAsync(Schedule.Id);
            }
        });
    }

    public int ScheduleId => Schedule?.Id ?? 0;

    public string Name => Schedule?.Name ?? string.Empty;

    public string SubTitle { get; private set; } = string.Empty;

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value) && !_isInitializing && Schedule != null)
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
                    _isEnabled = !newValue;
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
                _isEnabled = !newValue;
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
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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

    public async Task RefreshChapterNameAsync(bool force = false)
    {
        if (Schedule?.Id <= 0) return;
        // Capture to avoid null reference
        var schedule = Schedule;
        if (schedule == null) return;
        var scheduleId = schedule.Id;

        try
        {
            var displayName = await displayService.GetChapterDisplayNameAsync(scheduleId, force);
            
            if (!string.IsNullOrEmpty(displayName))
            {
                SubTitle = displayName;
                OnPropertyChanged(nameof(SubTitle));
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing chapter name for schedule {ScheduleId}", scheduleId);
        }
    }

    public void RefreshChapterName(bool force = false)
    {
        _ = RefreshChapterNameAsync(force);
    }

    public int CompareTo(object? obj)
    {
        return obj is not ScheduleListItem other ? 1 : ScheduleId.CompareTo(other.ScheduleId);
    }

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        if (Schedule?.Id <= 0) return;
        // Capture to avoid null reference
        var schedule = Schedule;
        if (schedule == null) return;
        var scheduleId = schedule.Id;

        // Find the updated schedule in the state
        var updatedSchedule = applicationState.Value.Schedules
            .FirstOrDefault(s => s.Id == scheduleId);

        if (updatedSchedule == null) return;

        // Check if the track/chapter changed by comparing BibleReadingSchedule or Music properties
        var trackChanged = false;

        if (_lastKnownSchedule?.BibleReadingSchedule != null && updatedSchedule.BibleReadingSchedule != null)
        {
            // Check if book or chapter changed
            if (_lastKnownSchedule.BibleReadingSchedule.BookNumber != updatedSchedule.BibleReadingSchedule.BookNumber ||
                _lastKnownSchedule.BibleReadingSchedule.ChapterNumber != updatedSchedule.BibleReadingSchedule.ChapterNumber)
            {
                trackChanged = true;
            }
        }
        else if (_lastKnownSchedule?.Music != null && updatedSchedule.Music != null)
        {
            // Check if track number changed
            if (_lastKnownSchedule.Music.TrackNumber != updatedSchedule.Music.TrackNumber)
            {
                trackChanged = true;
            }
        }

        // Store old values before updating
        var oldDaysOfWeek = Schedule?.DaysOfWeek ?? 0;
        var oldIsEnabled = _isEnabled;
        var oldName = Schedule?.Name ?? string.Empty;
        var oldHour = Schedule?.Hour ?? 0;
        var oldMinute = Schedule?.Minute ?? 0;
        
        // Update the schedule reference
        Schedule = updatedSchedule;
        _lastKnownSchedule = updatedSchedule;
        
        // Check if properties changed by comparing old values with new values
        var daysOfWeekChanged = oldDaysOfWeek != updatedSchedule.DaysOfWeek;
        var isEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled;
        var nameChanged = oldName != updatedSchedule.Name;
        var timeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute;

        if (trackChanged)
        {
            // Refresh the chapter name display
            RefreshChapterName();
        }

        // Always update _isEnabled to match the schedule
        _isEnabled = updatedSchedule.IsEnabled;

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