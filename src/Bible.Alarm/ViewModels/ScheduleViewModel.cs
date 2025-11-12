using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.General;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public class ScheduleViewModel : ObservableObject
{
    private readonly ILogger _logger;

    private readonly IToastService _popUpService;
    private readonly INavigation _navigation;
    private readonly ISchedulePersistenceService _schedulePersistenceService;
    private readonly IBibleNavigationService _bibleNavigationService;
    private readonly IMediaCacheSetupService _mediaCacheSetupService;
    private readonly INavigationService _navigationService;
    private readonly IScheduleSelectionService _scheduleSelectionService;
    private readonly IScheduleDisplayService _scheduleDisplayService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    public ICommand BatteryOptimizationExcludeCommand { get; private set; }
    public ICommand BatteryOptimizationDismissCommand { get; private set; }

    public ICommand PreviousBookCommand { get; set; }
    public ICommand NextBookCommand { get; set; }

    public ICommand PreviousChapterCommand { get; set; }
    public ICommand NextChapterCommand { get; set; }

    public ScheduleViewModel(
        ILogger logger,
        IToastService popUpService,
        INavigation navigation,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        IDispatcher dispatcher,
        IState<ApplicationState> state,
        ISchedulePersistenceService schedulePersistenceService,
        IBibleNavigationService bibleNavigationService,
        IMediaCacheSetupService mediaCacheSetupService,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IScheduleDisplayService scheduleDisplayService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _popUpService = popUpService;
        _navigation = navigation;
        _scopeFactory = scopeFactory;
        _dispatcher = dispatcher;
        _state = state;
        _schedulePersistenceService = schedulePersistenceService;
        _bibleNavigationService = bibleNavigationService;
        _mediaCacheSetupService = mediaCacheSetupService;
        _navigationService = navigationService;
        _scheduleSelectionService = scheduleSelectionService;
        _scheduleDisplayService = scheduleDisplayService;
        _serviceProvider = serviceProvider;
        
        var playbackService1 = playbackService;
        var notificationService1 = notificationService;

        // Subscribe to state changes to update when CurrentSchedule changes
        long lastScheduleId = -1;
        bool modelInitialized = false;
        EventHandler onCurrentScheduleChanged = null;
        onCurrentScheduleChanged = (sender, e) =>
        {
            var state = _state.Value;
            
            // Handle when CurrentSchedule is set (new or existing schedule)
            if (state.CurrentSchedule != null)
            {
                var currentScheduleId = state.CurrentSchedule.Id;
                
                // Update if this is a different schedule (by ID) or if not yet initialized
                if (currentScheduleId != lastScheduleId || !modelInitialized)
                {
                    _currentSchedule = state.CurrentSchedule;
                    lastScheduleId = currentScheduleId;

                    // Determine if this is a new schedule (Id <= 0) or existing
                    IsNewSchedule = _currentSchedule.Id <= 0;
                    SetModel(_currentSchedule);
                    modelInitialized = true;

                    IsBusy = false;
                }
            }
            // Handle when CurrentSchedule is null (creating a new schedule)
            else if (!modelInitialized && state.CurrentSchedule == null)
            {
                // Initialize with sample schedule for new schedule creation
                Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
                    SetModel(sampleSchedule);
                    modelInitialized = true;
                    IsNewSchedule = true;
                });
            }
        };
        _state.StateChanged += onCurrentScheduleChanged;
        
        // Trigger initial update based on current state
        onCurrentScheduleChanged(null, EventArgs.Empty);

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

            // Wait a moment for state change to propagate before navigating
            if (saved)
            {
                await Task.Delay(50); // Small delay to ensure state update is processed
                await _navigation.PopAsync();
            }

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

            await _navigationService.NavigateToMusicSelectionAsync();

            Music = await _scheduleSelectionService.LoadMusicForSelectionAsync(_scheduleId, IsNewSchedule, _musicUpdated, Music);

            _dispatcher.Dispatch(new MusicSelectionAction(Music));

            IsBusy = false;
        });

        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            await _navigationService.NavigateToBibleSelectionAsync();

            BibleReadingSchedule = await _scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                _scheduleId, IsNewSchedule, _bibleReadingUpdated, BibleReadingSchedule);

            if (BibleReadingSchedule != null)
            {
                RefreshChapterName();
            }

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
            await _navigationService.OpenNumberOfChaptersModalAsync(this);
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await _navigationService.CloseModalAsync();
            IsBusy = false;
        });

        SelectNumberOfChaptersCommand = new AsyncRelayCommand<NumberOfChaptersListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentNumberOfChapters != null) CurrentNumberOfChapters.IsSelected = false;

            CurrentNumberOfChapters = x;
            CurrentNumberOfChapters.IsSelected = true;

            await _navigationService.CloseModalAsync();

            IsBusy = false;
        });

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        BatteryOptimizationExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = _serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    await MarkBatteryOptimizationModalAsShown();
                    await _navigationService.CloseModalAsync();
                    batteryService.ShowOptimizationSettingsPage();
                }
            }
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();
            await _navigationService.CloseModalAsync();
        });

        PreviousBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            if (await _bibleNavigationService.MoveToPreviousBookAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        NextBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            if (await _bibleNavigationService.MoveToNextBookAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        PreviousChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            if (await _bibleNavigationService.MoveToPreviousChapterAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        NextChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;
            
            if (await _bibleNavigationService.MoveToNextChapterAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
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
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var batteryService = _serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                await batteryService.MarkModalAsShownAsync();
            }
        }
    }

    private async Task ShowBatteryOptimizationExclusionPage()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android) return;

        var batteryService = _serviceProvider.GetService<IBatteryOptimizationService>();
        if (batteryService == null) return;

        if (batteryService.CanShowOptimizeActivity()) CanOptimizeBattery = true;

        if (await batteryService.ShouldShowModalAsync())
        {
            using var scope = _scopeFactory.CreateScope();
            var modal = scope.ServiceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
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

    public AlarmMusic Music
    {
        get => Model.Music;
        set => Model.Music = value;
    }

    private bool _bibleReadingUpdated;

    public BibleReadingSchedule BibleReadingSchedule
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
        _ = _mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
    }

    private async Task<bool> SaveAsync()
    {
        if (!await Validate()) return false;

        if (IsNewSchedule) IsEnabled = true;

        var model = GetModel();
        
        // Don't pass music if it wasn't updated (for existing schedules)
        if (!IsNewSchedule && !_musicUpdated)
        {
            model.Music = null;
        }

        var saved = await _schedulePersistenceService.SaveScheduleAsync(model, IsNewSchedule, _musicUpdated, _bibleReadingUpdated);
        
        if (saved)
        {
            SetupMediaCache(model.Id);
        }

        return saved;
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
            await _schedulePersistenceService.DeleteScheduleAsync(_scheduleId);
        }
    }

    private void RefreshChapterName()
    {
        if (BibleReadingSchedule == null) return;
        
        _ = Task.Run(async () =>
        {
            try
            {
                var displayName = await _scheduleDisplayService.GetChapterDisplayNameForBibleReadingAsync(
                    _scheduleId, BibleReadingSchedule, true);
                
                if (!string.IsNullOrEmpty(displayName))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        BibleReadingTitleText = displayName;
                    });
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened while refreshing chapter name for schedule {ScheduleId}", _scheduleId);
            }
        });
    }

}