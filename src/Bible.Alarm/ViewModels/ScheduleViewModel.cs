using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
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

        _state = MauiAppHolder.Services.GetRequiredService<IState<ApplicationState>>();
        _dispatcher = MauiAppHolder.Services.GetRequiredService<IDispatcher>();

        _schedulePersistenceService = schedulePersistenceService;
        _bibleNavigationService = bibleNavigationService;
        _mediaCacheSetupService = mediaCacheSetupService;
        _navigationService = navigationService;
        _scheduleSelectionService = scheduleSelectionService;
        _scheduleDisplayService = scheduleDisplayService;
        _serviceProvider = serviceProvider;

        var lastScheduleId = -1;
        var modelInitialized = false;
        var isInitializingNewSchedule = false;

        _state.StateChanged += OnCurrentScheduleChanged;

        AlarmMusic lastMusic = null;
        BibleReadingSchedule lastBibleReading = null;

        _state.StateChanged += OnMusicChanged;
        _state.StateChanged += OnBibleReadingChanged;

        CancelCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            await _navigationService.PopAsync();

            IsBusy = false;
        });

        SaveCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            if (IsEnabled &&
                (DeviceInfo.Platform == DevicePlatform.iOS
                 || DeviceInfo.Platform == DevicePlatform.WinUI)
                && !await notificationService.CanSchedule())
                IsEnabled = false;

            if (!IsNewSchedule)
                if (playbackService.IsPrepared
                    && _scheduleId == playbackService.CurrentlyPlayingScheduleId)
                    await playbackService.Dismiss();

            var saved = await SaveAsync();


            if (saved)
            {
                await Task.Delay(50);
                await _navigationService.PopAsync();
            }

            if (saved && IsEnabled) await _popUpService.ShowScheduledNotification(Model);

            IsBusy = false;
        });

        DeleteCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            // If it's a new schedule, just navigate back without deleting
            if (IsNewSchedule)
            {
                await _navigationService.PopAsync();
                IsBusy = false;
                return;
            }

            // For existing schedules, delete and then navigate back
            if (playbackService.IsPrepared
                && _scheduleId == playbackService.CurrentlyPlayingScheduleId)
                await playbackService.Dismiss();

            await DeleteAsync();

            await _navigationService.PopAsync();

            IsBusy = false;
        });

        ToggleDayCommand = new RelayCommand<DaysOfWeek>(Toggle);

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        SelectMusicCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            Music = await _scheduleSelectionService.LoadMusicForSelectionAsync(_scheduleId, IsNewSchedule, _musicUpdated, Music);

            await _navigationService.NavigateToMusicSelectionAsync();

            _dispatcher.Dispatch(new MusicSelectionAction(Music));

            IsBusy = false;
        });

        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            BibleReadingSchedule = await _scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                _scheduleId, IsNewSchedule, _bibleReadingUpdated, BibleReadingSchedule);

            if (BibleReadingSchedule != null)
            {
                RefreshChapterName();
            }

            await _navigationService.NavigateToBibleSelectionAsync();

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

        void OnCurrentScheduleChanged(object sender, EventArgs e)
        {
            var stateValue = _state.Value;

            // Handle when CurrentSchedule is set (new or existing schedule)
            if (stateValue.CurrentSchedule != null)
            {
                var currentScheduleId = stateValue.CurrentSchedule.Id;

                if (currentScheduleId == lastScheduleId && modelInitialized) return;

                isInitializingNewSchedule = false;

                var currentSchedule = stateValue.CurrentSchedule;
                lastScheduleId = currentScheduleId;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var isNew = currentSchedule.Id <= 0;
                    IsNewSchedule = isNew;
                    SetModel(currentSchedule);
                    modelInitialized = true;
                    IsBusy = false;
                });
            }

            else if (!modelInitialized && !isInitializingNewSchedule && stateValue.CurrentSchedule == null)
            {
                isInitializingNewSchedule = true;
                Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var currentState = _state.Value;
                        if (isInitializingNewSchedule && !modelInitialized && currentState.CurrentSchedule == null)
                        {
                            SetModel(sampleSchedule);
                            modelInitialized = true;
                            IsNewSchedule = true;
                        }

                        isInitializingNewSchedule = false;
                    });
                });
            }
        }

        void OnBibleReadingChanged(object sender, EventArgs e)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null ||
                stateValue.CurrentBibleReadingSchedule == lastBibleReading ||
                stateValue.CurrentBibleReadingSchedule == BibleReadingSchedule) return;
            BibleReadingSchedule = stateValue.CurrentBibleReadingSchedule;
            lastBibleReading = stateValue.CurrentBibleReadingSchedule;
            _bibleReadingUpdated = true;
            RefreshChapterName();
        }

        // Define handlers BEFORE they're subscribed and BEFORE any return statement
        void OnMusicChanged(object sender, EventArgs e)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentMusic == null || stateValue.CurrentMusic == lastMusic ||
                stateValue.CurrentMusic == Music) return;
            Music = stateValue.CurrentMusic;
            lastMusic = stateValue.CurrentMusic;
            _musicUpdated = true;
        }
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
            await _navigationService.OpenBatteryOptimizationModalAsync(this);
        }
    }


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
        Name = model.Name; // Use property setter to trigger property change notification
        IsEnabled = model.IsEnabled;
        DaysOfWeek = model.DaysOfWeek;
        Time = new TimeSpan(model.Hour, model.Minute, model.Second);
        MusicEnabled = model.MusicEnabled;
        NotificationEnabled = model.NotificationEnabled;
        AlwaysPlayFromStart = model.AlwaysPlayFromStart;

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

    public Meridian Meridian => Time.Hours < 12 ? Meridian.Am : Meridian.Pm;

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

    private bool _isNewSchedule;
    private bool _isExistingSchedule;

    public bool IsNewSchedule
    {
        get => _isNewSchedule;
        private set
        {
            IsExistingSchedule = !value;
            SetProperty(ref _isNewSchedule, value);
        }
    }

    public bool IsExistingSchedule
    {
        get => _isExistingSchedule;
        private set => SetProperty(ref _isExistingSchedule, value);
    }

    private void Toggle(DaysOfWeek day)
    {
        if ((DaysOfWeek & day) == day)
            DaysOfWeek &= ~day;
        else
            DaysOfWeek |= day;

        OnPropertyChanged(nameof(DaysOfWeek));
    }

    private void SetupMediaCache(int scheduleId)
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
        if (_scheduleId > 0)
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