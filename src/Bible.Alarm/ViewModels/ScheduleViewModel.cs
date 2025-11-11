using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public class ScheduleViewModel : ObservableObject
{
    private readonly ILogger _logger;

    private readonly IAlarmService _alarmService;
    private readonly IToastService _popUpService;
    private readonly INavigation _navigation;

    public Command BatteryOptimizationExcludeCommand { get; private set; }
    public Command BatteryOptimizationDismissCommand { get; private set; }

    public ICommand PreviousBookCommand { get; set; }
    public ICommand NextBookCommand { get; set; }

    public ICommand PreviousChapterCommand { get; set; }
    public ICommand NextChapterCommand { get; set; }

    private readonly IBatteryOptimizationManager _batteryOptimizationManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    public ScheduleViewModel(
        ILogger logger,
        IToastService popUpService,
        IAlarmService alarmService,
        INavigation navigation,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IDispatcher dispatcher,
        IState<ApplicationState> state,
        IBatteryOptimizationManager batteryOptimizationManager = null)
    {
        _logger = logger;
        _popUpService = popUpService;
        _alarmService = alarmService;
        _navigation = navigation;
        var playbackService1 = playbackService;
        var notificationService1 = notificationService;
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
        _dispatcher = dispatcher;
        _state = state;
        
        if (DeviceInfo.Platform == DevicePlatform.Android)
            _batteryOptimizationManager = batteryOptimizationManager;

        //set schedules from initial state.
        //this should fire only once (look at the where condition).
        AlarmSchedule lastSchedule = null;
        bool modelInitialized = false;
        EventHandler onScheduleInitialized = null;
        onScheduleInitialized = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentSchedule != null && state.CurrentSchedule != lastSchedule)
            {
                _currentSchedule = state.CurrentSchedule;
                lastSchedule = _currentSchedule;

                IsNewSchedule = false;
                SetModel(_currentSchedule);
                modelInitialized = true;

                IsBusy = false;
                
                // Unsubscribe after first call
                _state.StateChanged -= onScheduleInitialized;
            }
            else if (!modelInitialized && state.CurrentSchedule == null)
            {
                // Initialize with sample schedule if state doesn't have CurrentSchedule
                Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
                    SetModel(sampleSchedule);
                    modelInitialized = true;
                    IsNewSchedule = true;
                });
                
                // Unsubscribe after initialization
                _state.StateChanged -= onScheduleInitialized;
            }
        };
        _state.StateChanged += onScheduleInitialized;

        AlarmMusic lastMusic = null;
        EventHandler onMusicChanged = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentMusic != null && state.CurrentMusic != lastMusic && state.CurrentMusic != Music)
            {
                Music = state.CurrentMusic;
                lastMusic = state.CurrentMusic;
                _musicUpdated = true;
            }
        };
        _state.StateChanged += onMusicChanged;

        BibleReadingSchedule lastBibleReading = null;
        EventHandler onBibleReadingChanged = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentBibleReadingSchedule != null && state.CurrentBibleReadingSchedule != lastBibleReading && state.CurrentBibleReadingSchedule != BibleReadingSchedule)
            {
                BibleReadingSchedule = state.CurrentBibleReadingSchedule;
                lastBibleReading = state.CurrentBibleReadingSchedule;
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        };
        _state.StateChanged += onBibleReadingChanged;

        CancelCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            await _navigation.PopAsync();

            IsBusy = false;
        });

        SaveCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            if (IsEnabled &&
                (DeviceInfo.Platform == DevicePlatform.iOS
                 || DeviceInfo.Platform == DevicePlatform.WinUI)
                && !await notificationService1.CanSchedule())
                IsEnabled = false;

            if (!IsNewSchedule)
                if (playbackService1.IsPrepared
                    && _scheduleId == playbackService1.CurrentlyPlayingScheduleId)
                    await playbackService1.Dismiss();

            var saved = await SaveAsync();

            if (saved) await _navigation.PopAsync();

            if (saved && IsEnabled) await _popUpService.ShowScheduledNotification(Model);

            IsBusy = false;
        });

        DeleteCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            if (playbackService1.IsPrepared
                && _scheduleId == playbackService1.CurrentlyPlayingScheduleId)
                await playbackService1.Dismiss();

            await DeleteAsync();

            await _navigation.PopAsync();

            IsBusy = false;
        });

        ToggleDayCommand = new RelayCommand<DaysOfWeek>(x => { Toggle(x); });

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        SelectMusicCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            var viewModel = _serviceProvider.GetRequiredService<MusicSelectionViewModel>();
            var page = _serviceProvider.GetRequiredService<MusicSelection>();
            page.BindingContext = viewModel;
            await _navigation.PushAsync(page);

            await Task.Run(async () =>
            {
                //get the latest music track
                if (Music == null || (!IsNewSchedule && !_musicUpdated))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    Music = await scheduleDbContext.AlarmMusic
                        .AsNoTracking()
                        .FirstAsync(x => x.AlarmScheduleId == _scheduleId);
                }
            });


            _dispatcher.Dispatch(new MusicSelectionAction(Music));

            IsBusy = false;
        });

        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            var viewModel = _serviceProvider.GetRequiredService<BibleSelectionViewModel>();
            var page = _serviceProvider.GetRequiredService<BibleSelection>();
            page.BindingContext = viewModel;
            await _navigation.PushAsync(page);

            await Task.Run(async () =>
            {
                //get the latest bible track
                if (BibleReadingSchedule == null || (!IsNewSchedule && !_bibleReadingUpdated))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    BibleReadingSchedule = await scheduleDbContext.BibleReadingSchedules
                        .AsNoTracking()
                        .FirstAsync(x => x.AlarmScheduleId == _scheduleId);

                    RefreshChapterName();
                }
            });

            _dispatcher.Dispatch(new BibleSelectionAction(
                BibleReadingSchedule,
                new BibleReadingSchedule
                {
                    PublicationCode = BibleReadingSchedule?.PublicationCode ?? "",
                    LanguageCode = BibleReadingSchedule?.LanguageCode ?? ""
                }));

            IsBusy = false;
        });


        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            var modal = _serviceProvider.GetRequiredService<NumberOfChaptersModal>();
            modal.BindingContext = this;
            await _navigation.PushModalAsync(modal);
            IsBusy = false;
        });


        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
            IsBusy = false;
        });

        SelectNumberOfChaptersCommand = new AsyncRelayCommand<NumberOfChaptersListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentNumberOfChapters != null) CurrentNumberOfChapters.IsSelected = false;

            CurrentNumberOfChapters = x;
            CurrentNumberOfChapters.IsSelected = true;

            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }

            IsBusy = false;
        });

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        BatteryOptimizationExcludeCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();

            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }

            _batteryOptimizationManager.ShowBatteryOptimizationExclusionSettingsPage();
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();

            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
        });

        PreviousBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            using var scope = _scopeFactory.CreateScope();
            using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextBook = await playlistService.GetPreviousBibleBook(BibleReadingSchedule.LanguageCode,
                BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber);

            if (nextBook.Value == null) return;
            
            BibleReadingSchedule.BookNumber = nextBook.Value.Number;
            BibleReadingSchedule.ChapterNumber = 1;
            BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
            _bibleReadingUpdated = true;
            RefreshChapterName();
        });

        NextBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            using var scope = _scopeFactory.CreateScope();
            using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextBook = await playlistService.GetNextBibleBook(BibleReadingSchedule.LanguageCode,
                BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber);

            if (nextBook.Value == null) return;
            
            BibleReadingSchedule.BookNumber = nextBook.Value.Number;
            BibleReadingSchedule.ChapterNumber = 1;
            BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
            _bibleReadingUpdated = true;
            RefreshChapterName();
        });

        PreviousChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            using var scope = _scopeFactory.CreateScope();
            using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var prevChapter = await playlistService.GetPreviousBibleChapter(BibleReadingSchedule.LanguageCode,
                BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber,
                BibleReadingSchedule.ChapterNumber);

            if (prevChapter.Key == null || prevChapter.Value == null) return;
            
            BibleReadingSchedule.BookNumber = prevChapter.Key.Number;
            BibleReadingSchedule.ChapterNumber = prevChapter.Value.Number;
            BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
            _bibleReadingUpdated = true;
            RefreshChapterName();
        });

        NextChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            using var scope = _scopeFactory.CreateScope();
            using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextChapter = await playlistService.GetNextBibleChapter(BibleReadingSchedule.LanguageCode,
                BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber,
                BibleReadingSchedule.ChapterNumber);

            if (nextChapter.Key == null || nextChapter.Value == null) return;
            
            BibleReadingSchedule.BookNumber = nextChapter.Key.Number;
            BibleReadingSchedule.ChapterNumber = nextChapter.Value.Number;
            BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
            _bibleReadingUpdated = true;
            RefreshChapterName();
        });
    }

    private bool _canOptimizeBattery;

    public bool CanOptimizeBattery
    {
        get => _canOptimizeBattery;
        set => SetProperty(ref _canOptimizeBattery, value);
    }

    private async Task MarkBatteryOptimizationModalAsShown()
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        if (!await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
        {
            await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
            {
                Key = "AndroidBatteryOptimizationExclusionPromptShown",
                Value = "True"
            });

            await scheduleDbContext.SaveChangesAsync();
        }
    }

    private async Task ShowBatteryOptimizationExclusionPage()
    {
        if (_batteryOptimizationManager.CanShowOptimizeActivity()) CanOptimizeBattery = true;

        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        if (!await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
        {
            var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
            modal.BindingContext = this;
            await _navigation.PushModalAsync(modal);
        }
    }


    private AlarmSchedule _currentSchedule;

    public ICommand CancelCommand { get; set; }

    public ICommand SaveCommand { get; set; }
    public ICommand DeleteCommand { get; set; }

    public ICommand SelectMusicCommand { get; set; }
    public ICommand SelectBibleCommand { get; set; }

    public ICommand NotificationEnabledCommand { get; set; }

    public ICommand ToggleDayCommand { get; set; }
    public ICommand ToggleAlwaysPlayFromStartCommand { get; set; }
    public AlarmSchedule Model { get; private set; }

    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectNumberOfChaptersCommand { get; set; }

    private ObservableCollection<NumberOfChaptersListViewItemModel> _numberOfChaptersList;

    public ObservableCollection<NumberOfChaptersListViewItemModel> NumberOfChaptersList
    {
        get => _numberOfChaptersList;
        set => SetProperty(ref _numberOfChaptersList, value);
    }

    private NumberOfChaptersListViewItemModel _currentNumberOfChapters;

    public NumberOfChaptersListViewItemModel CurrentNumberOfChapters
    {
        get => _currentNumberOfChapters;
        set => SetProperty(ref _currentNumberOfChapters, value);
    }

    private void PopulateNumberOfChaptersListView(AlarmSchedule model)
    {
        var chapterVMs = new ObservableCollection<NumberOfChaptersListViewItemModel>();

        for (var i = 1; i <= 21; i++)
        {
            var chaptersVm = new NumberOfChaptersListViewItemModel(i);

            if (model.NumberOfChaptersToRead == i)
            {
                chaptersVm.IsSelected = true;
                CurrentNumberOfChapters = chaptersVm;
            }

            chapterVMs.Add(chaptersVm);
        }

        NumberOfChaptersList = chapterVMs;
    }

    private AlarmSchedule GetModel()
    {
        Model.Id = _scheduleId;

        Model.Name = Name;
        Model.IsEnabled = IsEnabled;
        Model.DaysOfWeek = DaysOfWeek;
        Model.Hour = Time.Hours;
        Model.Minute = Time.Minutes;
        Model.MusicEnabled = MusicEnabled;
        Model.NotificationEnabled = _notificationEnabled;
        Model.AlwaysPlayFromStart = AlwaysPlayFromStart;
        Model.NumberOfChaptersToRead = CurrentNumberOfChapters.Value;

        return Model;
    }

    private void SetModel(AlarmSchedule model)
    {
        Model = model.DeepClone();

        _scheduleId = model.Id;
        _name = model.Name;
        _isEnabled = model.IsEnabled;
        _daysOfWeek = model.DaysOfWeek;
        _time = new TimeSpan(model.Hour, model.Minute, model.Second);
        _musicEnabled = model.MusicEnabled;
        _notificationEnabled = model.NotificationEnabled;
        _alwaysPlayFromStart = model.AlwaysPlayFromStart;

        PopulateNumberOfChaptersListView(model);
        RefreshChapterName();
    }

    private int _scheduleId;

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private DaysOfWeek _daysOfWeek;

    public DaysOfWeek DaysOfWeek
    {
        get => _daysOfWeek;
        set => SetProperty(ref _daysOfWeek, value);
    }

    private TimeSpan _time;

    public TimeSpan Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }

    public string Hour => (Time.Hours % 12).ToString("D2");

    public string Minute => Time.Minutes.ToString("D2");

    public Meridien Meridien => Time.Hours < 12 ? Meridien.Am : Meridien.Pm;

    private bool _musicEnabled;

    public bool MusicEnabled
    {
        get => _musicEnabled;
        set => SetProperty(ref _musicEnabled, value);
    }

    private bool _notificationEnabled;

    public bool NotificationEnabled
    {
        get => _notificationEnabled;
        set
        {
            if (!value) _ = ShowBatteryOptimizationExclusionPage();

            SetProperty(ref _notificationEnabled, value);
        }
    }

    private bool _alwaysPlayFromStart;

    public bool AlwaysPlayFromStart
    {
        get => _alwaysPlayFromStart;
        set => SetProperty(ref _alwaysPlayFromStart, value);
    }

    private bool _musicUpdated;

    public AlarmMusic? Music
    {
        get => Model.Music;
        set => Model.Music = value;
    }

    private bool _bibleReadingUpdated;

    public BibleReadingSchedule? BibleReadingSchedule
    {
        get => Model.BibleReadingSchedule;
        set => Model.BibleReadingSchedule = value;
    }

    private string _bibleReadingTitleText;

    public string BibleReadingTitleText
    {
        get => _bibleReadingTitleText;
        set => SetProperty(ref _bibleReadingTitleText, value);
    }

    public bool IsNewSchedule { get; private set; }
    public bool IsExistingSchedule => !IsNewSchedule;

    private void Toggle(DaysOfWeek day)
    {
        if ((DaysOfWeek & day) == day)
            DaysOfWeek &= ~day;
        else
            DaysOfWeek |= day;

        OnPropertyChanged(nameof(DaysOfWeek));
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

    private async Task<bool> SaveAsync()
    {
        if (!await Validate()) return false;

        if (IsNewSchedule) IsEnabled = true;

        var model = GetModel();

        if (IsNewSchedule)
        {
            await Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                await scheduleDbContext.AlarmSchedules.AddAsync(model);
                await scheduleDbContext.SaveChangesAsync();
                if (model.IsEnabled) await _alarmService.Create(model);
            });

            using var scope = _scopeFactory.CreateScope();
            _dispatcher.Dispatch(new AddScheduleAction(model));
        }
        else
        {
            await Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                
                var existing = await scheduleDbContext.AlarmSchedules
                    .Include(x => x.Music)
                    .Include(x => x.BibleReadingSchedule)
                    .FirstAsync(x => x.Id == model.Id);

                existing.Hour = model.Hour;
                existing.Minute = model.Minute;
                existing.DaysOfWeek = model.DaysOfWeek;
                existing.IsEnabled = model.IsEnabled;

                if (model.Music != null && existing.Music != null && _musicUpdated)
                {
                    existing.Music.Repeat = model.Music.Repeat;
                    existing.Music.LanguageCode = model.Music.LanguageCode;
                    existing.Music.MusicType = model.Music.MusicType;
                    existing.Music.PublicationCode = model.Music.PublicationCode;
                    existing.Music.TrackNumber = model.Music.TrackNumber;
                }

                if (model.BibleReadingSchedule != null && existing.BibleReadingSchedule != null)
                {
                    existing.BibleReadingSchedule.BookNumber = model.BibleReadingSchedule.BookNumber;
                    existing.BibleReadingSchedule.ChapterNumber = model.BibleReadingSchedule.ChapterNumber;
                    existing.BibleReadingSchedule.LanguageCode = model.BibleReadingSchedule.LanguageCode;
                    existing.BibleReadingSchedule.PublicationCode = model.BibleReadingSchedule.PublicationCode;

                    if (_bibleReadingUpdated) existing.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                }

                existing.MusicEnabled = model.MusicEnabled;
                existing.NotificationEnabled = model.NotificationEnabled;
                existing.AlwaysPlayFromStart = model.AlwaysPlayFromStart;
                existing.NumberOfChaptersToRead = model.NumberOfChaptersToRead;
                existing.Name = model.Name;
                existing.Second = model.Second;
                existing.SnoozeMinutes = model.SnoozeMinutes;

                await scheduleDbContext.SaveChangesAsync();
                _alarmService.Update(model);
            });

            // Update the current schedule reference
            _currentSchedule = model;

            _dispatcher.Dispatch(new UpdateScheduleAction(model));
        }

        SetupMediaCache(model.Id);

        return true;
    }

    private async Task<bool> Validate()
    {
        if (DaysOfWeek == 0)
        {
            await _popUpService.ShowMessage("Please select day(s) of Week.");
            return false;
        }

        return true;
    }

    private async Task DeleteAsync()
    {
        if (_scheduleId >= 0)
        {
            await Task.Run(async () =>
            {
                _alarmService.Delete(_scheduleId);
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                var model = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == _scheduleId);
                scheduleDbContext.AlarmSchedules.Remove(model);
                await scheduleDbContext.SaveChangesAsync();
            });

            _dispatcher.Dispatch(new RemoveScheduleAction(_currentSchedule));
        }
    }

    private void RefreshChapterName()
    {
        if (BibleReadingSchedule == null) return;
        
        using var scope = _scopeFactory.CreateScope();
        var syncContext = scope.ServiceProvider.GetRequiredService<TaskScheduler>();

        Task.Run(async () =>
            {
                try
                {
                    await using var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                    var bookName = await mediaDbContext.BibleBook
                        .Where(x => x.BibleTranslation.Code == BibleReadingSchedule.PublicationCode
                                    && x.BibleTranslation.Language.Code == BibleReadingSchedule.LanguageCode
                                    && x.Number == BibleReadingSchedule.BookNumber)
                        .Select(x => x.Name)
                        .AsNoTracking()
                        .FirstOrDefaultAsync();

                    return bookName;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "An error happened in refreshChapterName task under schedule view model.");
                }

                return null;
            })
            .ContinueWith(x =>
            {
                if (!x.IsCompleted || BibleReadingSchedule == null) return;
                try
                {
                    BibleReadingTitleText = $"{x.Result} {BibleReadingSchedule.ChapterNumber}";
                }
                catch (Exception e)
                {
                    _logger.Error(e,
                        "An error happened in refreshChapterName task continue with under schedule view model.");
                }
            }, syncContext);
    }

}