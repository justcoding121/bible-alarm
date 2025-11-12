using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Shared.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.ViewModels;

public class ScheduleListItem(
    ILogger logger,
    ISchedulePlaybackService playbackService,
    IScheduleDisplayService displayService,
    IScheduleStateService scheduleStateService,
    IPlaylistService playlistService)
    : ObservableObject, IComparable, IDisposable, IRecipient<TrackChangedMessage>
{
    private bool _isInitializing;
    private bool _isRegistered;

    public AlarmSchedule Schedule { get; private set; }

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
        OnPropertyChanged(nameof(DaysOfWeek));
        OnPropertyChanged(nameof(IsEnabled));

        if (!_isRegistered)
        {
            WeakReferenceMessenger.Default.Register(this);
            _isRegistered = true;
        }

        PlayCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0)
            {
                await playbackService.PlayScheduleAsync(Schedule.Id);
            }
        });

        _ = RefreshChapterNameAsync(true);

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync())
            {
                await playlistService.MoveToPreviousBibleChapter(Schedule.Id);
                await RefreshChapterNameAsync(true);
            }
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id > 0 && await playbackService.CanMoveChapterAsync())
            {
                await playlistService.MoveToNextBibleChapter(Schedule.Id);
                await RefreshChapterNameAsync(true);
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
            var success = await scheduleStateService.UpdateScheduleEnabledStateAsync(ScheduleId, newValue);
            
            if (!success)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _isEnabled = !newValue;
                    OnPropertyChanged(nameof(IsEnabled));
                });
            }
            else
            {
                RaisePropertiesChangedEvent();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while handling IsEnabled change for schedule {ScheduleId}", ScheduleId);
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isEnabled = !newValue;
                OnPropertyChanged(nameof(IsEnabled));
            });
        }
    }

    public DaysOfWeek DaysOfWeek => Schedule?.DaysOfWeek ?? 0;

    public string TimeText => Schedule?.TimeText ?? string.Empty;

    public string Hour => Schedule?.MeridianHour.ToString("D2") ?? "00";

    public string Minute => Schedule?.Minute.ToString("D2") ?? "00";

    public Meridian Meridian => Schedule?.Meridian ?? Meridian.Am;

    public ScheduleListItem This => this;

    public ICommand PlayCommand { get; private set; }

    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }

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

        try
        {
            var displayName = await displayService.GetChapterDisplayNameAsync(Schedule.Id, force);
            
            if (!string.IsNullOrEmpty(displayName))
            {
                SubTitle = displayName;
                OnPropertyChanged(nameof(SubTitle));
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing chapter name for schedule {ScheduleId}", Schedule.Id);
        }
    }

    public void RefreshChapterName(bool force = false)
    {
        _ = RefreshChapterNameAsync(force);
    }

    public int CompareTo(object obj)
    {
        return obj is not ScheduleListItem other ? 1 : ScheduleId.CompareTo(other.ScheduleId);
    }

    public void Receive(TrackChangedMessage message)
    {
        if (Schedule != null && message.Value == Schedule.Id)
        {
            RefreshChapterName();
        }
    }

    public void Dispose()
    {
        if (!_isRegistered) return;
        WeakReferenceMessenger.Default.Unregister<TrackChangedMessage>(this);
        _isRegistered = false;
    }
}