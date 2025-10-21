using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Utilities;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions;
using Microsoft.EntityFrameworkCore;
using Mvvmicro;
using Serilog;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.ViewModels;

public class HomeViewModel : ViewModel, IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private ScheduleDbContext _scheduleDbContext;
    private MediaDbContext _mediaDbContext;

    private IToastService _popUpService;
    private INavigationService _navigationService;
    private IMediaCacheService _mediaCacheService;
    private IAlarmService _alarmService;

    private INotificationService _notificationService;

    private List<IDisposable> _subscriptions = [];


    public HomeViewModel(
        ILogger logger,
        ScheduleDbContext scheduleDbContext,
        IToastService popUpService, INavigationService navigationService,
        IMediaCacheService mediaCacheService,
        IAlarmService alarmService,
        INotificationService notificationService,
        MediaDbContext mediaDbContext,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scheduleDbContext = scheduleDbContext;
        _popUpService = popUpService;
        _navigationService = navigationService;
        _mediaCacheService = mediaCacheService;
        _alarmService = alarmService;
        _notificationService = notificationService;
        _mediaDbContext = mediaDbContext;
        _scopeFactory = scopeFactory;

        _subscriptions.Add(scheduleDbContext);

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
        var subscription = ReduxContainer.Store
            .Select(state => state.Schedules)
            .Where(x => x != null)
            .DistinctUntilChanged()
            .Subscribe(x =>
            {
                Schedules = x;
                ListenIsEnabledChanges();
                IsBusy = false;
            });
        _subscriptions.Add(subscription);

        Initialize();
    }

    private async Task SeedDefaultAlarm()
    {
        if (!await _scheduleDbContext.AlarmSchedules.AnyAsync()
            && !await _scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == AppConstants.GeneralSettingsKeys.AlarmSeeded)
            //for existing apps before version 1.30
            && !await _scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == AppConstants.GeneralSettingsKeys.AndroidBatteryOptimizationExclusionPromptShown))
        {
            var schedule = await AlarmSchedule.GetSampleSchedule(false, _mediaDbContext);

            await _scheduleDbContext.AlarmSchedules.AddAsync(schedule);
            await _scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
            {
                Key = AppConstants.GeneralSettingsKeys.AlarmSeeded,
                Value = "True"
            });

            await _scheduleDbContext.SaveChangesAsync();
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
    private SemaphoreSlim _lock = new(1);

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

                    var alarmSchedules = await _scheduleDbContext.AlarmSchedules
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

                            await _scheduleDbContext.SaveChangesAsync();
                        }
                    }

                    var initialSchedules = new ObservableHashSet<ScheduleListItem>();
                    using var scope = _scopeFactory.CreateScope();
                    foreach (var schedule in alarmSchedules) 
                        initialSchedules.Add(new ScheduleListItem(schedule, scope.ServiceProvider.GetRequiredService<ILogger>(), _scopeFactory));

                    ReduxContainer.Store.Dispatch(new InitializeAction { ScheduleList = initialSchedules });

                    _initialized = true;
                }
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
        var scheduleListChangedObservable = Observable.FromEventPattern(
            (EventHandler<NotifyCollectionChangedEventArgs> ev)
                => new NotifyCollectionChangedEventHandler(ev),
            ev => Schedules.CollectionChanged += ev,
            ev => Schedules.CollectionChanged -= ev);

        //for schedules currently shown on screen.
        var isEnabledObservable = Schedules.Select(item =>
        {
            var removedObservable = scheduleListChangedObservable.Any(z =>
            {
                var oldItem = z.EventArgs.OldItems?.Cast<ScheduleListItem>();
                return oldItem != null && oldItem.Any(removed => item == removed);
            });

            //observe until the schedule is removed from the list.
            return Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, ScheduleListItem>>(
                    onNextHandler => (object sender, PropertyChangedEventArgs e)
                        => onNextHandler(
                            new KeyValuePair<string, ScheduleListItem>(e.PropertyName, (ScheduleListItem)sender)),
                    handler => item.PropertyChanged += handler,
                    handler => item.PropertyChanged -= handler)
                .TakeUntil(removedObservable)
                .Where(kv => kv.Key == "IsEnabled")
                .Select(y => y.Value);
        }).Merge();

        //observe for all future schedules. 
        var isEnableObservableForNewSchedules = scheduleListChangedObservable
            .SelectMany(x =>
            {
                var newItems = x.EventArgs.NewItems?.Cast<ScheduleListItem>();
                if (newItems == null) return Enumerable.Empty<IObservable<ScheduleListItem>>();

                //observe until the schedule is removed from the list.
                return newItems.Select(added =>
                {
                    var removedObservable = scheduleListChangedObservable.Any(z =>
                    {
                        var oldItem = z.EventArgs.OldItems?.Cast<ScheduleListItem>();
                        return oldItem != null && oldItem.Any(removed => added == removed);
                    });

                    return Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, ScheduleListItem>>(
                            onNextHandler => (object sender, PropertyChangedEventArgs e)
                                => onNextHandler(
                                    new KeyValuePair<string, ScheduleListItem>(e.PropertyName,
                                        (ScheduleListItem)sender)),
                            handler => added.PropertyChanged += handler,
                            handler => added.PropertyChanged -= handler)
                        .TakeUntil(removedObservable)
                        .Where(kv => kv.Key == "IsEnabled")
                        .Select(y => y.Value);
                });
            })
            .Merge();

        //now the actual job (show the scheduled notification).
        var subscription = Observable.Merge(isEnabledObservable, isEnableObservableForNewSchedules)
            .ObserveOn(Scheduler.CurrentThread)
            .Do(async y =>
            {
                IsBusy = true;

                if (y.IsEnabled &&
                    (DeviceInfo.Platform == DevicePlatform.iOS
                     || DeviceInfo.Platform == DevicePlatform.WinUI)
                    && !await _notificationService.CanSchedule())
                {
                    y.IsEnabled = false;

                    if (DeviceInfo.Platform == DevicePlatform.iOS)
                        await _popUpService.ShowMessage(
                            "Cannot schedule alarm because you've disabled notifications. " +
                            "Please enable notification for this app under system settings.", 7);
                    else
                        await _popUpService.ShowMessage(
                            "Cannot schedule alarm because you've denied backgroud apps permission. " +
                            "Please grant background apps permission for this app under system settings.", 7);

                    IsBusy = false;
                    return;
                }

                await Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    var existing = await scheduleDbContext.AlarmSchedules.FirstAsync(x => x.Id == y.ScheduleId);
                    existing.IsEnabled = y.IsEnabled;
                    await scheduleDbContext.SaveChangesAsync();

                    _alarmService.Update(existing);
                });

                if (y.IsEnabled) await _popUpService.ShowScheduledNotification(y.Schedule);

                SetupMediaCache(y.Schedule.Id);

                y.RaisePropertiesChangedEvent();

                IsBusy = false;
            })
            .Subscribe();

        _subscriptions.Add(subscription);
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
        _subscriptions.ForEach(x => x.Dispose());

        _scheduleDbContext.Dispose();
        _popUpService.Dispose();
        _mediaCacheService.Dispose();
        _alarmService.Dispose();
        _notificationService.Dispose();
        _mediaDbContext.Dispose();

        _lock.Dispose();
    }
}

public class ScheduleListItem : ViewModel, IComparable, IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AlarmSchedule Schedule;
    private IDisposable _subscription;

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