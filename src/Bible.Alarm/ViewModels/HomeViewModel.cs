using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Utilities;
using Bible.Alarm.Shared.Services.Infrastructure.Media;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Bible.Alarm.Common.Mvvm.Messenger;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Contracts.Scheduler;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Infrastructure.Media;
using Bible.Alarm.Services.Infrastructure.Schedule;
using Bible.Alarm.ViewModels.Redux.Actions.Schedule;

namespace Bible.Alarm.ViewModels;

public class HomeViewModel : ViewModel, IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IToastService _popUpService;
    private readonly INavigationService _navigationService;
    private readonly IMediaCacheService _mediaCacheService;
    private readonly IAlarmService _alarmService;

    private readonly INotificationService _notificationService;

    private readonly List<IDisposable> _subscriptions = [];
    private readonly Dictionary<ScheduleListItem, PropertyChangedEventHandler> _isEnabledHandlers = [];


    public HomeViewModel(
        ILogger logger,
        IToastService popUpService, INavigationService navigationService,
        IMediaCacheService mediaCacheService,
        IAlarmService alarmService,
        INotificationService notificationService,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _popUpService = popUpService;
        _navigationService = navigationService;
        _mediaCacheService = mediaCacheService;
        _alarmService = alarmService;
        _notificationService = notificationService;
        _scopeFactory = scopeFactory;

        AddScheduleCommand = new Command(async () =>
        {
            ReduxContainer.Store.Dispatch(new ViewScheduleAction());
            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ScheduleViewModel>();
            await _navigationService.Navigate(viewModel);
        });

        ViewScheduleCommand = new Command<ScheduleListItem>(async x =>
        {
            x.Schedule.IsEnabled = x.IsEnabled;

            ReduxContainer.Store.Dispatch(new ViewScheduleAction
            {
                SelectedScheduleListItem = x
            });

            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ScheduleViewModel>();
            await _navigationService.Navigate(viewModel);
        });


        //set schedules from initial state.
        //this should fire only once (look at the where condition).
        ObservableHashSet<ScheduleListItem> lastSchedules = null;
        var subscription = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.Schedules != null && state.Schedules != lastSchedules)
            {
                Schedules = state.Schedules;
                lastSchedules = state.Schedules;
                ListenIsEnabledChanges();
                IsBusy = false;
            }
        });
        _subscriptions.Add(subscription);

        Initialize();
    }

    private async Task SeedDefaultAlarm()
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        if (!await scheduleDbContext.AlarmSchedules.AnyAsync()
            && !await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == AppConstants.GeneralSettingsKeys.AlarmSeeded)
            //for existing apps before version 1.30
            && !await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == AppConstants.GeneralSettingsKeys.AndroidBatteryOptimizationExclusionPromptShown))
        {
            var schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

            await scheduleDbContext.AlarmSchedules.AddAsync(schedule);
            await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
            {
                Key = AppConstants.GeneralSettingsKeys.AlarmSeeded,
                Value = "True"
            });

            await scheduleDbContext.SaveChangesAsync();
        }
    }

    private ObservableHashSet<ScheduleListItem> _schedules;

    public ObservableHashSet<ScheduleListItem> Schedules
    {
        get => _schedules;
        set => this.Set(ref _schedules, value);
    }

    private bool _isBusy = true;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            this.Set(ref _isBusy, value);
            Loaded = !_isBusy;
        }
    }


    private bool _loaded = false;

    public bool Loaded
    {
        get => _loaded;
        set => this.Set(ref _loaded, value);
    }

    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }

    private ScheduleViewModel _selectedSchedule;

    public ScheduleViewModel SelectedSchedule
    {
        get => _selectedSchedule;
        set => this.Set(ref _selectedSchedule, value);
    }

    private bool _initialized = false;
    private readonly SemaphoreSlim _lock = new(1);

    private void Initialize()
    {
        Messenger<bool>.Subscribe(MvvmMessages.Initialized, async vm =>
        {
            await _lock.WaitAsync();

            try
            {
                if (!_initialized)
                {
                    await SeedDefaultAlarm();

                    using var scope = _scopeFactory.CreateScope();
                    var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    
                    var alarmSchedules = await scheduleDbContext.AlarmSchedules
                        .Include(x => x.BibleReadingSchedule)
                        .Include(x => x.Music)
                        .ToListAsync();

                    if (DeviceInfo.Platform == DevicePlatform.Android)
                    {
                        //bible gateway is not supported anymore due to copyright issues
                        var toRemove = alarmSchedules.Where(x =>
                            BgSourceHelper.PublicationCodeToNameMappings.Any(y =>
                                y.Key == x.BibleReadingSchedule.PublicationCode)).ToList();

                        if (toRemove.Any())
                        {
                            foreach (var item in toRemove)
                            {
                                item.BibleReadingSchedule.PublicationCode = "bi12";
                                item.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                            }

                            await scheduleDbContext.SaveChangesAsync();
                        }
                    }

                    var initialSchedules = new ObservableHashSet<ScheduleListItem>();
                    foreach (var schedule in alarmSchedules)
                        initialSchedules.Add(new ScheduleListItem(schedule,
                            scope.ServiceProvider.GetRequiredService<ILogger>(), _scopeFactory));

                    ReduxContainer.Store.Dispatch(new InitializeAction { ScheduleList = initialSchedules });

                    _initialized = true;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened in HomeViewModel Initialize.");
            }
            finally
            {
                try
                {
                    _lock.Release();
                }
                catch (ObjectDisposedException e)
                {
                    _logger.Error(e, "HomeViewModel: @lock disposed error.");
                }
            }
        }, true);
    }

    private void ListenIsEnabledChanges()
    {
        // Subscribe to existing schedules
        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                SubscribeToIsEnabledChanges(item);
            }
        }

        // Subscribe to collection changes
        if (Schedules != null)
        {
            Schedules.CollectionChanged += (sender, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (ScheduleListItem item in e.NewItems)
                    {
                        SubscribeToIsEnabledChanges(item);
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (ScheduleListItem item in e.OldItems)
                    {
                        UnsubscribeFromIsEnabledChanges(item);
                    }
                }
            };
        }
    }

    private void SubscribeToIsEnabledChanges(ScheduleListItem item)
    {
        PropertyChangedEventHandler handler = async (sender, e) =>
        {
            if (e.PropertyName == "IsEnabled" && sender is ScheduleListItem scheduleItem)
            {
                await HandleIsEnabledChanged(scheduleItem);
            }
        };

        item.PropertyChanged += handler;
        _isEnabledHandlers[item] = handler;
    }

    private void UnsubscribeFromIsEnabledChanges(ScheduleListItem item)
    {
        if (_isEnabledHandlers.TryGetValue(item, out var handler))
        {
            item.PropertyChanged -= handler;
            _isEnabledHandlers.Remove(item);
        }
    }

    private async Task HandleIsEnabledChanged(ScheduleListItem scheduleItem)
    {
        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);

        if (scheduleItem.IsEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS
             || DeviceInfo.Platform == DevicePlatform.WinUI)
            && !await _notificationService.CanSchedule())
        {
            scheduleItem.IsEnabled = false;

            if (DeviceInfo.Platform == DevicePlatform.iOS)
                await _popUpService.ShowMessage(
                    "Cannot schedule alarm because you've disabled notifications. " +
                    "Please enable notification for this app under system settings.", 7);
            else
                await _popUpService.ShowMessage(
                    "Cannot schedule alarm because you've denied backgroud apps permission. " +
                    "Please grant background apps permission for this app under system settings.", 7);

            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            return;
        }

        await Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            var existing = await scheduleDbContext.AlarmSchedules.FirstAsync(x => x.Id == scheduleItem.ScheduleId);
            existing.IsEnabled = scheduleItem.IsEnabled;
            await scheduleDbContext.SaveChangesAsync();

            _alarmService.Update(existing);
        });

        if (scheduleItem.IsEnabled) await _popUpService.ShowScheduledNotification(scheduleItem.Schedule);

        SetupMediaCache(scheduleItem.Schedule.Id);

        scheduleItem.RaisePropertiesChangedEvent();

        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
    }

    private void SetupMediaCache(long scheduleId)
    {
        Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                using var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
                await mediaCacheService.SetupAlarmCache(scheduleId);
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened in SetupAlarmCache task.");
            }
        });
    }


    public void Dispose()
    {
        // Unsubscribe from all schedule IsEnabled changes
        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                UnsubscribeFromIsEnabledChanges(item);
            }
        }
        
        _subscriptions.ForEach(x => x.Dispose());
        _isEnabledHandlers.Clear();

        _lock.Dispose();
        
        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // _popUpService (IToastService), _mediaCacheService (IMediaCacheService), 
        // _alarmService (IAlarmService), and _notificationService (INotificationService) 
        // are singletons and should not be disposed here as they are managed by the DI container
    }
}

public class ScheduleListItem : ViewModel, IComparable, IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AlarmSchedule Schedule;
    private readonly IDisposable _subscription;

    public ScheduleListItem(AlarmSchedule schedule, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        Schedule = schedule;
        _isEnabled = schedule.IsEnabled;

        PlayCommand = new Command(() =>
        {
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                using var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();

                try
                {
                    if (Schedule.Id > 0)
                    {
                        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

                        await toastService.ShowMessage("Your schedule will start playing in a few seconds.", 5);

                        using var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        await notificationService.ShowNotification(Schedule.Id);
                    }
                }
                catch (Exception e)
                {
                    _logger.Information(e, "An error happenned when playing alarm.");
                    await toastService.ShowMessage("Error. Network may not be available." +
                                                   "Please try again.", 5);
                }
            });
        });

        RefreshChapterName(true);

        _subscription = Messenger<object>.Subscribe(MvvmMessages.TrackChanged,
            (x) =>
            {
                if ((int)x == Schedule.Id) RefreshChapterName();

                return Task.CompletedTask;
            });

        PreviousCommand = new Command(async () =>
        {
            if (await CanMove(schedule.Id))
            {
                using var scope = _scopeFactory.CreateScope();
                using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
                await playlistService.MoveToPreviousBibleChapter(schedule.Id);

                RefreshChapterName(true);
            }
        });

        NextCommand = new Command(async () =>
        {
            if (await CanMove(schedule.Id))
            {
                using var scope = _scopeFactory.CreateScope();
                using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
                await playlistService.MoveToNextBibleChapter(schedule.Id);

                RefreshChapterName(true);
            }
        });
    }

    private async Task<bool> CanMove(long scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

        if (!playbackService.IsPlaying) return true;

        using var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();

        await toastService.ShowMessage("Cannot update the chapter when schedule is in progress.");

        return false;
    }

    public long ScheduleId => Schedule.Id;

    public string Name => Schedule.Name;

    public string SubTitle { get; private set; }

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.Set(ref _isEnabled, value);
    }

    public DaysOfWeek DaysOfWeek => Schedule.DaysOfWeek;

    public string TimeText => Schedule.TimeText;

    public string Hour => Schedule.MeridienHour.ToString("D2");

    public string Minute => Schedule.Minute.ToString("D2");

    public Meridien Meridien => Schedule.Meridien;

    public ScheduleListItem This => this;

    public ICommand PlayCommand { get; private set; }

    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }

    public void RaisePropertiesChangedEvent()
    {
        RaiseProperties(GetType()
            .GetProperties()
            .Where(x => x.Name != "IsEnabled")
            .Select(x => x.Name).ToArray());
    }

    public void RefreshChapterName(bool force = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var syncContext = scope.ServiceProvider.GetRequiredService<TaskScheduler>();

        _ = Task.Run(async () =>
            {
                try
                {
                    var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

                    if (Schedule == null || (!force && !playbackService.IsPrepared)) return null;

                    using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

                    var schedule = await scheduleDbContext.AlarmSchedules
                        .Include(x => x.BibleReadingSchedule)
                        .AsNoTracking()
                        .Where(x => x.Id == Schedule.Id)
                        .FirstOrDefaultAsync();

                    if (schedule != null)
                    {
                        using var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                        var bookName = await mediaDbContext.BibleBook
                            .Where(x => x.BibleTranslation.Code == schedule.BibleReadingSchedule.PublicationCode
                                        && x.BibleTranslation.Language.Code ==
                                        schedule.BibleReadingSchedule.LanguageCode
                                        && x.Number == schedule.BibleReadingSchedule.BookNumber)
                            .Select(x => x.Name)
                            .AsNoTracking()
                            .FirstOrDefaultAsync();

                        if (bookName != null)
                        {
                            Schedule.BibleReadingSchedule.BookNumber = schedule.BibleReadingSchedule.BookNumber;
                            Schedule.BibleReadingSchedule.ChapterNumber = schedule.BibleReadingSchedule.ChapterNumber;
                            return new Tuple<string, int>(bookName, schedule.BibleReadingSchedule.ChapterNumber);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "An error happened in RefreshChapterName task under list item.");
                }

                return null;
            })
            .ContinueWith((x) =>
            {
                try
                {
                    if (x.IsCompleted && x.Result != null)
                    {
                        SubTitle = $"{x.Result.Item1} {x.Result.Item2}";
                        RaiseProperty("SubTitle");
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "An error happened in RefreshChapterName continue with task under list item.");
                }
            }, syncContext);
    }

    public int CompareTo(object obj)
    {
        return ScheduleId.CompareTo((obj as ScheduleListItem).ScheduleId);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}