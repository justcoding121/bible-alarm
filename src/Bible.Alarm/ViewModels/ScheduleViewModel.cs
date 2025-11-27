using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public class ScheduleViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;

    private readonly IToastService _popUpService;
    private readonly ISchedulePersistenceService _schedulePersistenceService;
    private readonly IMediaCacheSetupService _mediaCacheSetupService;
    private readonly INavigationService _navigationService;
    private readonly IScheduleDisplayService _scheduleDisplayService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IState<ApplicationState> _state;
    private readonly IState<PlaybackState> _playbackState;
    private readonly IDispatcher _dispatcher;
    private readonly IServiceScopeFactory _scopeFactory;

    private int _lastScheduleId = -1;
    private bool _modelInitialized;
    private bool _isInitializingNewSchedule;
    private AlarmMusic _lastMusic;
    private BibleReadingSchedule _lastBibleReading;

    public ICommand BatteryOptimizationExcludeCommand { get; private set; }
    public ICommand BatteryOptimizationDismissCommand { get; private set; }

    public ICommand PreviousBookCommand { get; set; }
    public ICommand NextBookCommand { get; set; }

    public ICommand PreviousChapterCommand { get; set; }
    public ICommand NextChapterCommand { get; set; }

    private readonly ScheduleItemStateService _scheduleItemStateService;

    public ScheduleViewModel(
        ILogger logger,
        IToastService popUpService,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        ISchedulePersistenceService schedulePersistenceService,
        IBibleNavigationService bibleNavigationService,
        IMediaCacheSetupService mediaCacheSetupService,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IScheduleDisplayService scheduleDisplayService,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        ScheduleItemStateService scheduleItemStateService)
    {
        _logger = logger;
        _popUpService = popUpService;
        _scopeFactory = scopeFactory;

        _state = state;
        _playbackState = playbackState;
        _dispatcher = dispatcher;
        _scheduleItemStateService = scheduleItemStateService;

        _schedulePersistenceService = schedulePersistenceService;
        var bibleNavigationService1 = bibleNavigationService;
        _mediaCacheSetupService = mediaCacheSetupService;
        _navigationService = navigationService;
        var scheduleSelectionService1 = scheduleSelectionService;
        _scheduleDisplayService = scheduleDisplayService;
        _serviceProvider = serviceProvider;

        _state.StateChanged += OnStateChanged;
        _state.StateChanged += OnMusicChanged;
        _state.StateChanged += OnBibleReadingChanged;

        // Set IsBusy to true by default so the page shows loading indicator immediately
        IsBusy = true;
        
        // Check if schedule is already in state (e.g., if state changed before this ViewModel was created)
        // This ensures we load the schedule immediately if it's already available
        // Check synchronously first, then also set up a delayed check as fallback
        var currentState = _state.Value;
        
        if (currentState.CurrentSchedule != null)
        {
            // Schedule is already in state, trigger the handler immediately on main thread
            MainThread.BeginInvokeOnMainThread(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
        }
        else
        {
            // Schedule not in state yet, set up a delayed check as fallback
            // This handles the case where the action is dispatched but state hasn't updated yet
            _ = Task.Run(async () =>
            {
                // Wait a bit for the state to update after action dispatch
                await Task.Delay(100);
                var delayedState = _state.Value;
                if (delayedState.CurrentSchedule != null && !_modelInitialized)
                {
                    // Schedule is now in state, trigger the handler
                    await MainThread.InvokeOnMainThreadAsync(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
                }
            });
        }
        
        // Safety fallback: ensure IsBusy is set to false after a maximum delay
        // This prevents the overlay from staying visible indefinitely if something goes wrong
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000); // 2 second timeout
            if (IsBusy && !_modelInitialized)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    // Hide Home page overlay as safety fallback
                    _dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
                });
            }
        });

        CancelCommand = new AsyncRelayCommand(async () =>
        {
            // Cancel just navigates back, no need for progress indicator
            await _navigationService.NavigateToHomeAsync();
        });

        SaveCommand = new AsyncRelayCommand(async () =>
        {
            // Show overlay immediately via state
            _dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
            // Wait for state to update and UI to reflect the change
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Force property change notification
                OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
                // Wait a bit to ensure UI has rendered the overlay
                await Task.Delay(50);
            });

            if (IsEnabled &&
                (DeviceInfo.Platform == DevicePlatform.iOS
                 || DeviceInfo.Platform == DevicePlatform.WinUI)
                && !await notificationService.CanScheduleAsync())
                IsEnabled = false;

            if (!IsNewSchedule)
                if (_playbackState.Value.IsPreparingOrPlaying
                    && _scheduleId == _playbackState.Value.CurrentScheduleId)
                    await playbackService.StopAsync();

            var saved = await SaveAsync();

            if (saved)
            {
                await _navigationService.NavigateToHomeAsync();
                // Note: Schedule page overlay will be hidden when Home page Appearing event fires
            }
            else
            {
                // Hide overlay if save failed
                _dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            }

            if (saved && IsEnabled) await _popUpService.ShowScheduledNotification(Model);
        });

        DeleteCommand = new AsyncRelayCommand(async () =>
        {
            // Show overlay immediately via state
            _dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
            // Wait for state to update and UI to reflect the change
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Force property change notification
                OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
                // Wait a bit to ensure UI has rendered the overlay
                await Task.Delay(50);
            });

            // If it's a new schedule, just navigate back without deleting
            if (IsNewSchedule)
            {
                await _navigationService.NavigateToHomeAsync();
                // Note: Schedule page overlay will be hidden when navigating back
                return;
            }

            // For existing schedules, delete and then navigate back
            if (_playbackState.Value.IsPreparingOrPlaying
                && _scheduleId == _playbackState.Value.CurrentScheduleId)
                await playbackService.StopAsync();

            await DeleteAsync();

            await _navigationService.NavigateToHomeAsync();
            // Note: Schedule page overlay will be hidden when navigating back
        });

        ToggleDayCommand = new RelayCommand<DaysOfWeek>(Toggle);

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        SelectMusicCommand = new AsyncRelayCommand(async () =>
        {
            Music = await scheduleSelectionService1.LoadMusicForSelectionAsync(_scheduleId, IsNewSchedule, _musicUpdated, Music);

            await _navigationService.NavigateToMusicSelectionAsync();

            _dispatcher.Dispatch(new MusicSelectionAction(Music));
        });

        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            BibleReadingSchedule = await scheduleSelectionService1.LoadBibleReadingForSelectionAsync(
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
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            await _navigationService.OpenNumberOfChaptersModalAsync(this);
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await _navigationService.PopModalAsync();
        });

        SelectNumberOfChaptersCommand = new AsyncRelayCommand<NumberOfChaptersListViewItemModel>(async x =>
        {
            if (CurrentNumberOfChapters != null) CurrentNumberOfChapters.IsSelected = false;

            CurrentNumberOfChapters = x;
            CurrentNumberOfChapters.IsSelected = true;

            await _navigationService.PopModalAsync();
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
                    await _navigationService.PopModalAsync();
                    batteryService.ShowOptimizationSettingsPage();
                }
            }
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();
            await _navigationService.PopModalAsync();
        });

        PreviousBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;

            if (await bibleNavigationService1.MoveToPreviousBookAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        NextBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;

            if (await bibleNavigationService1.MoveToNextBookAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        PreviousChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;

            if (await bibleNavigationService1.MoveToPreviousChapterAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        NextChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null) return;

            if (await bibleNavigationService1.MoveToNextChapterAsync(BibleReadingSchedule))
            {
                _bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        var stateValue = _state.Value;
        
        // Notify about overlay visibility changes
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
        });
        
        // Continue with existing schedule change handling
        OnCurrentScheduleChanged(sender, e);
    }

    private void OnCurrentScheduleChanged(object sender, EventArgs e)
    {
        var stateValue = _state.Value;

        // Handle when CurrentSchedule is set (new or existing schedule)
        if (stateValue.CurrentSchedule != null)
        {
            var currentScheduleId = stateValue.CurrentSchedule.Id;

            if (currentScheduleId == _lastScheduleId && _modelInitialized) 
            {
                // Check if the schedule in Schedules collection has been updated (e.g., from next/prev in home view)
                var updatedSchedule = stateValue.Schedules?.FirstOrDefault(s => s.Id == currentScheduleId);
                if (updatedSchedule != null && updatedSchedule != Model)
                {
                    // Schedule was updated externally (e.g., next/prev from home view)
                    // Update the model to reflect the changes
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetModel(updatedSchedule);
                        RefreshChapterName();
                    });
                }
                return;
            }

            _isInitializingNewSchedule = false;

            var currentSchedule = stateValue.CurrentSchedule;
            _lastScheduleId = currentScheduleId;

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // IsBusy is already true from constructor, no need to set it again
                    var isNew = currentSchedule.Id <= 0;
                    IsNewSchedule = isNew;
                    SetModel(currentSchedule);
                    _modelInitialized = true;
                    // Small delay to ensure UI has rendered the content before hiding overlay
                    await Task.Delay(100);
                    IsBusy = false;
                    // Explicitly notify property change to ensure UI updates
                    OnPropertyChanged(nameof(IsBusy));
                    // Note: Home page overlay will be hidden when Schedule page Appearing event fires
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in OnCurrentScheduleChanged handler");
                    // Ensure IsBusy is set to false even if there's an error
                    IsBusy = false;
                    OnPropertyChanged(nameof(IsBusy));
                    // Note: Home page overlay will be hidden when Schedule page Appearing event fires
                }
            });
        }
        else if (!_modelInitialized && !_isInitializingNewSchedule && stateValue.CurrentSchedule == null)
        {
            _isInitializingNewSchedule = true;
            MainThread.BeginInvokeOnMainThread(() => IsBusy = true); // Show busy indicator during initialization
            Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var currentState = _state.Value;
                    if (_isInitializingNewSchedule && !_modelInitialized && currentState.CurrentSchedule == null)
                    {
                        SetModel(sampleSchedule);
                        _modelInitialized = true;
                        IsNewSchedule = true;
                    }

                    _isInitializingNewSchedule = false;
                    IsBusy = false; // Hide busy indicator after initialization
                    // Note: Home page overlay will be hidden when Schedule page Appearing event fires
                });
            });
        }
    }

    private void OnBibleReadingChanged(object sender, EventArgs e)
    {
        if (Model == null) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null ||
            stateValue.CurrentBibleReadingSchedule == _lastBibleReading ||
            stateValue.CurrentBibleReadingSchedule == BibleReadingSchedule) return;
        BibleReadingSchedule = stateValue.CurrentBibleReadingSchedule;
        _lastBibleReading = stateValue.CurrentBibleReadingSchedule;
        _bibleReadingUpdated = true;
        RefreshChapterName();
    }

    private void OnMusicChanged(object sender, EventArgs e)
    {
        if (Model == null) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null || stateValue.CurrentMusic == _lastMusic ||
            stateValue.CurrentMusic == Music) return;
        Music = stateValue.CurrentMusic;
        _lastMusic = stateValue.CurrentMusic;
        _musicUpdated = true;
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
        Name = model.Name;
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

    private void SetupMediaCache(int scheduleId, bool isUpdate = false)
    {
        if (isUpdate)
        {
            // For updates, delete old cache files first in a separate task, then cache new files
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
                    await mediaCacheService.DeleteScheduleCacheAsync(scheduleId);
                    await _mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error deleting old cache and setting up new cache for schedule {ScheduleId}", scheduleId);
                }
            });
        }
        else
        {
            // For new schedules, just cache files
            _ = _mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
        }
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
            SetupMediaCache(model.Id, isUpdate: !IsNewSchedule);
        }

        return saved;
    }

    private async Task<bool> Validate()
    {
        if (DaysOfWeek != 0) return true;
        await _popUpService.ShowMessage("Please select day(s) of Week.");
        return false;

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

    /// <summary>
    /// Hides the Home page overlay. Called when the Schedule page is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay()
    {
        _dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
    }

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// This property is bound to the Schedule page overlay.
    /// </summary>
    public bool IsSchedulePageOverlayVisible => _state.Value.IsSchedulePageOverlayVisible;

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay()
    {
        _dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
    }

    public void Dispose()
    {
        _state.StateChanged -= OnStateChanged;
        _state.StateChanged -= OnMusicChanged;
        _state.StateChanged -= OnBibleReadingChanged;
    }
}