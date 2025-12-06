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
    private bool _isSaving;
    private AlarmMusic _lastMusic;
    private BibleReadingSchedule _lastBibleReading;
    private bool _isScrolledToBottom;

    public ICommand BatteryOptimizationExcludeCommand { get; private set; }
    public ICommand BatteryOptimizationDismissCommand { get; private set; }

    public ICommand PreviousBookCommand { get; set; }
    public ICommand NextBookCommand { get; set; }

    public ICommand PreviousChapterCommand { get; set; }
    public ICommand NextChapterCommand { get; set; }

    private readonly IScheduleItemStateService _scheduleItemStateService;

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
        IScheduleItemStateService scheduleItemStateService)
    {
        var constructorStartTime = DateTime.UtcNow;
        _logger = logger;
        _logger.Information("[PERF] ScheduleViewModel: Constructor started at {StartTime}", constructorStartTime);
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
        var checkStateStartTime = DateTime.UtcNow;
        var currentState = _state.Value;
        var checkStateElapsed = (DateTime.UtcNow - checkStateStartTime).TotalMilliseconds;
        _logger.Information("[PERF] ScheduleViewModel: State check took {ElapsedMs}ms, CurrentSchedule={HasSchedule}", checkStateElapsed, currentState.CurrentSchedule != null);
        
        if (currentState.CurrentSchedule != null)
        {
            // Schedule is already in state, trigger the handler immediately on main thread
            _logger.Information("[PERF] ScheduleViewModel: Schedule already in state, triggering OnCurrentScheduleChanged");
            MainThread.BeginInvokeOnMainThread(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
        }
        else
        {
            // Schedule not in state yet, set up a delayed check as fallback
            // This handles the case where the action is dispatched but state hasn't updated yet
            _logger.Information("[PERF] ScheduleViewModel: Schedule not in state, setting up delayed check");
            _ = Task.Run(async () =>
            {
                // Wait a bit for the state to update after action dispatch
                await Task.Delay(100);
                var delayedState = _state.Value;
                if (delayedState.CurrentSchedule != null && !_modelInitialized)
                {
                    // Schedule is now in state, trigger the handler
                    _logger.Information("[PERF] ScheduleViewModel: Schedule found after delay, triggering OnCurrentScheduleChanged");
                    await MainThread.InvokeOnMainThreadAsync(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
                }
            });
        }
        
        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        _logger.Information("[PERF] ScheduleViewModel: Constructor completed in {ElapsedMs}ms", constructorElapsed);
        
        // Safety fallback: ensure IsBusy is set to false after a maximum delay
        // This prevents the overlay from staying visible indefinitely if something goes wrong
        _ = Task.Run(async () =>
        {
            // 2 second timeout
            await Task.Delay(2000);
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
            _logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
                IsNewSchedule, _scheduleId, Name);

            // Set saving flag to prevent ViewModel reset during save
            _isSaving = true;
            
            try
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
                    _logger.Information("SaveCommand: Save successful, navigating to home. ScheduleId={ScheduleId}", _scheduleId);
                    // Wait a bit to ensure the state update from AddScheduleAction has propagated
                    // This ensures the new schedule appears in the Home page list
                    await Task.Delay(100);
                    await _navigationService.NavigateToHomeAsync();
                    // Note: Schedule page overlay will be hidden when Home page Appearing event fires
                }
                else
                {
                    _logger.Error("SaveCommand: Save failed, hiding overlay. ScheduleId={ScheduleId}", _scheduleId);
                    // Hide overlay if save failed
                    _dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                }

                if (saved && IsEnabled) await _popUpService.ShowScheduledNotification(Model);
            }
            finally
            {
                // Reset saving flag after save completes
                _isSaving = false;
            }
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

        ToggleDayCommand = new RelayCommand<object>(ToggleDay);

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
            
            // Update the model immediately so the UI reflects the change
            if (Model != null && CurrentNumberOfChapters != null)
            {
                Model.NumberOfChaptersToRead = CurrentNumberOfChapters.Value;
            }
            
            // Explicitly notify property changes to ensure UI binding updates
            // The setter already notifies CurrentNumberOfChaptersText, but we'll do it again to be sure
            OnPropertyChanged(nameof(CurrentNumberOfChapters));
            OnPropertyChanged(nameof(CurrentNumberOfChaptersText));

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
        var handlerStartTime = DateTime.UtcNow;
        _logger.Information("[PERF] OnCurrentScheduleChanged: Handler started at {StartTime}", handlerStartTime);
        
        var stateValue = _state.Value;

        // Handle when CurrentSchedule is set (new or existing schedule)
        if (stateValue.CurrentSchedule != null)
        {
            var currentScheduleId = stateValue.CurrentSchedule.Id;
            _logger.Information("[PERF] OnCurrentScheduleChanged: Processing schedule Id={ScheduleId}, LastScheduleId={LastScheduleId}, ModelInitialized={ModelInitialized}", 
                currentScheduleId, _lastScheduleId, _modelInitialized);

            if (currentScheduleId == _lastScheduleId && _modelInitialized) 
            {
                // Don't update the model if we're currently saving, as this would overwrite user changes
                if (_isSaving)
                {
                    _logger.Debug("OnCurrentScheduleChanged: Skipping model update during save operation. ScheduleId={ScheduleId}", currentScheduleId);
                    return;
                }
                
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
            
            var isNew = currentSchedule.Id <= 0;
            _logger.Information("[PERF] OnCurrentScheduleChanged: IsNew={IsNew}, invoking on main thread", isNew);

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var mainThreadStartTime = DateTime.UtcNow;
                    _logger.Information("[PERF] OnCurrentScheduleChanged: Main thread handler started at {StartTime}", mainThreadStartTime);
                    
                    // IsBusy is already true from constructor, no need to set it again
                    IsNewSchedule = isNew;
                    
                    var setModelStartTime = DateTime.UtcNow;
                    SetModel(currentSchedule);
                    var setModelElapsed = (DateTime.UtcNow - setModelStartTime).TotalMilliseconds;
                    _logger.Information("[PERF] OnCurrentScheduleChanged: SetModel took {ElapsedMs}ms", setModelElapsed);
                    
                    _modelInitialized = true;
                    // Small delay to ensure UI has rendered the content before hiding overlay
                    await Task.Delay(100);
                    IsBusy = false;
                    // Explicitly notify property change to ensure UI updates
                    OnPropertyChanged(nameof(IsBusy));
                    
                    var mainThreadElapsed = (DateTime.UtcNow - mainThreadStartTime).TotalMilliseconds;
                    _logger.Information("[PERF] OnCurrentScheduleChanged: Main thread handler completed in {ElapsedMs}ms", mainThreadElapsed);
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
        else if (stateValue.CurrentSchedule == null)
        {
            // CurrentSchedule is null - this means we're creating a new schedule
            // Reset the ViewModel state to ensure it's properly initialized for a new schedule
            // BUT: Don't reset if we're currently saving, as this would interfere with the save operation
            if (_modelInitialized && !_isSaving)
            {
                _logger.Debug("OnCurrentScheduleChanged: Resetting ViewModel for new schedule. Previous ScheduleId={PreviousScheduleId}",
                    _scheduleId);
                _modelInitialized = false;
                _lastScheduleId = -1;
                _scheduleId = 0;
                // Will be set to true after initialization
                IsNewSchedule = false;
            }
            
            if (!_modelInitialized && !_isInitializingNewSchedule)
            {
                _isInitializingNewSchedule = true;
                _logger.Information("[PERF] OnCurrentScheduleChanged: Starting new schedule initialization");
                // Show busy indicator during initialization
                MainThread.BeginInvokeOnMainThread(() => IsBusy = true);
                Task.Run(async () =>
                {
                    var getSampleStartTime = DateTime.UtcNow;
                    _logger.Information("[PERF] OnCurrentScheduleChanged: GetSampleSchedule started at {StartTime}", getSampleStartTime);
                    
                    using var scope = _scopeFactory.CreateScope();
                    var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
                    
                    var getSampleElapsed = (DateTime.UtcNow - getSampleStartTime).TotalMilliseconds;
                    _logger.Information("[PERF] OnCurrentScheduleChanged: GetSampleSchedule completed in {ElapsedMs}ms", getSampleElapsed);
                    
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var currentState = _state.Value;
                        if (_isInitializingNewSchedule && !_modelInitialized && currentState.CurrentSchedule == null)
                        {
                            _logger.Debug("OnCurrentScheduleChanged: Initializing new schedule. SampleSchedule.Id={SampleScheduleId}",
                                sampleSchedule.Id);
                            
                            var setModelStartTime = DateTime.UtcNow;
                            SetModel(sampleSchedule);
                            var setModelElapsed = (DateTime.UtcNow - setModelStartTime).TotalMilliseconds;
                            _logger.Information("[PERF] OnCurrentScheduleChanged: SetModel for new schedule took {ElapsedMs}ms", setModelElapsed);
                            
                            _modelInitialized = true;
                            IsNewSchedule = true;
                            _logger.Information("OnCurrentScheduleChanged: New schedule initialized. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}",
                                _scheduleId, IsNewSchedule);
                        }

                        _isInitializingNewSchedule = false;
                        // Hide busy indicator after initialization
                        IsBusy = false;
                        // Note: Home page overlay will be hidden when Schedule page Appearing event fires
                    });
                });
            }
        }
    }

    private void OnBibleReadingChanged(object sender, EventArgs e)
    {
        if (Model == null) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null) return;
        
        // Check if the bible reading actually changed by comparing properties
        var newBibleReading = stateValue.CurrentBibleReadingSchedule;
        var hasChanged = _lastBibleReading == null || 
                        BibleReadingSchedule == null ||
                        _lastBibleReading.LanguageCode != newBibleReading.LanguageCode ||
                        _lastBibleReading.PublicationCode != newBibleReading.PublicationCode ||
                        _lastBibleReading.BookNumber != newBibleReading.BookNumber ||
                        _lastBibleReading.ChapterNumber != newBibleReading.ChapterNumber ||
                        (BibleReadingSchedule != null && 
                         (BibleReadingSchedule.BookNumber != newBibleReading.BookNumber ||
                          BibleReadingSchedule.ChapterNumber != newBibleReading.ChapterNumber));
        
        if (!hasChanged) return;
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BibleReadingSchedule = newBibleReading;
            _lastBibleReading = newBibleReading;
            _bibleReadingUpdated = true;
            RefreshChapterName();
            OnPropertyChanged(nameof(BibleReadingSchedule));
            OnPropertyChanged(nameof(BibleReadingTitleText));
        });
    }

    private void OnMusicChanged(object sender, EventArgs e)
    {
        if (Model == null) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null) return;
        
        // Check if the music actually changed by comparing properties
        var newMusic = stateValue.CurrentMusic;
        var hasChanged = _lastMusic == null || 
                        Music == null ||
                        _lastMusic.LanguageCode != newMusic.LanguageCode ||
                        _lastMusic.PublicationCode != newMusic.PublicationCode ||
                        _lastMusic.MusicType != newMusic.MusicType ||
                        _lastMusic.TrackNumber != newMusic.TrackNumber ||
                        (Music != null && 
                         (Music.TrackNumber != newMusic.TrackNumber ||
                          Music.MusicType != newMusic.MusicType ||
                          Music.LanguageCode != newMusic.LanguageCode ||
                          Music.PublicationCode != newMusic.PublicationCode));
        
        if (!hasChanged) return;
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Music = newMusic;
            _lastMusic = newMusic;
            _musicUpdated = true;
            OnPropertyChanged(nameof(Music));
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
        set
        {
            if (SetProperty(ref _currentNumberOfChapters, value))
            {
                // Notify that the Text property (computed from CurrentNumberOfChapters) has changed
                OnPropertyChanged(nameof(CurrentNumberOfChaptersText));
            }
        }
    }
    
    /// <summary>
    /// Computed property for binding to the number of chapters text in the UI.
    /// This ensures the UI updates when CurrentNumberOfChapters changes.
    /// </summary>
    public string CurrentNumberOfChaptersText => CurrentNumberOfChapters?.Text ?? string.Empty;

    private void PopulateNumberOfChaptersListView(AlarmSchedule model)
    {
        // Preserve the current selection if user has made one (to prevent SetModel from overwriting user's choice)
        var preservedSelection = CurrentNumberOfChapters?.Value;
        
        var chapterVMs = new ObservableCollection<NumberOfChaptersListViewItemModel>();

        for (var i = 1; i <= 21; i++)
        {
            var chaptersVm = new NumberOfChaptersListViewItemModel(i);

            // If user has made a selection, use that; otherwise use the model's value
            var shouldSelect = preservedSelection.HasValue 
                ? preservedSelection.Value == i 
                : model.NumberOfChaptersToRead == i;

            if (shouldSelect)
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
        _logger.Debug("GetModel: Reading ViewModel properties. ViewModel.Name={ViewModelName}, ViewModel.MusicEnabled={ViewModelMusicEnabled}, ViewModel.IsEnabled={ViewModelIsEnabled}",
            Name, MusicEnabled, IsEnabled);
        
        Model.Id = _scheduleId;

        Model.Name = Name;
        Model.IsEnabled = IsEnabled;
        Model.DaysOfWeek = DaysOfWeek;
        Model.Hour = Time.Hours;
        Model.Minute = Time.Minutes;
        Model.MusicEnabled = MusicEnabled;
        Model.NotificationEnabled = _notificationEnabled;
        Model.AlwaysPlayFromStart = AlwaysPlayFromStart;
        
        // Use CurrentNumberOfChapters.Value if available, otherwise keep the Model's existing value
        // This ensures we always save the currently selected value, even if SetModel() was called and reset it
        if (CurrentNumberOfChapters != null)
        {
            var newValue = CurrentNumberOfChapters.Value;
            Model.NumberOfChaptersToRead = newValue;
            _logger.Debug("GetModel: Setting NumberOfChaptersToRead from CurrentNumberOfChapters.Value = {Value} (Model had {OldValue})", 
                newValue, Model.NumberOfChaptersToRead);
        }
        else
        {
            _logger.Warning("GetModel: CurrentNumberOfChapters is null, keeping existing Model.NumberOfChaptersToRead = {Value}", Model.NumberOfChaptersToRead);
        }

        _logger.Debug("GetModel: After setting Model properties. Model.Id={ModelId}, Model.Name={ModelName}, Model.MusicEnabled={ModelMusicEnabled}, Model.IsEnabled={ModelIsEnabled}, Model.DaysOfWeek={ModelDaysOfWeek}",
            Model.Id, Model.Name, Model.MusicEnabled, Model.IsEnabled, Model.DaysOfWeek);

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
        set
        {
            _logger.Debug("Name: Setting value from '{OldValue}' to '{NewValue}'", _name, value);
            SetProperty(ref _name, value);
        }
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
        set
        {
            _logger.Debug("MusicEnabled: Setting value from {OldValue} to {NewValue}", _musicEnabled, value);
            SetProperty(ref _musicEnabled, value);
        }
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

    public bool IsScrolledToBottom
    {
        get => _isScrolledToBottom;
        set => SetProperty(ref _isScrolledToBottom, value);
    }

    public bool IsExistingSchedule
    {
        get => _isExistingSchedule;
        private set => SetProperty(ref _isExistingSchedule, value);
    }

    private void ToggleDay(object parameter)
    {
        DaysOfWeek day;
        
        if (parameter is DaysOfWeek dayEnum)
        {
            day = dayEnum;
        }
        else if (parameter is string dayString && Enum.TryParse<DaysOfWeek>(dayString, out var parsedDay))
        {
            day = parsedDay;
        }
        else
        {
            // Invalid parameter
            return;
        }
        
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
        _logger.Information("SaveAsync: Starting save. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}, ModelInitialized={ModelInitialized}",
            IsNewSchedule, _scheduleId, Name, _modelInitialized);

        if (!_modelInitialized)
        {
            _logger.Error("SaveAsync: Model not initialized. Cannot save.");
            await _popUpService.ShowMessage("Schedule data is not ready. Please try again.");
            return false;
        }

        if (Model == null)
        {
            _logger.Error("SaveAsync: Model is null. Cannot save.");
            await _popUpService.ShowMessage("Schedule data is missing. Please try again.");
            return false;
        }

        if (!IsNewSchedule && _scheduleId <= 0)
        {
            _logger.Error("SaveAsync: Invalid ScheduleId for existing schedule. ScheduleId={ScheduleId}", _scheduleId);
            await _popUpService.ShowMessage("Invalid schedule ID. Please try again.");
            return false;
        }

        if (!await Validate())
        {
            _logger.Warning("SaveAsync: Validation failed");
            return false;
        }

        if (IsNewSchedule) IsEnabled = true;

        var model = GetModel();
        _logger.Debug("SaveAsync: Model retrieved. Model.Id={ModelId}, Model.Name={ModelName}, HasMusic={HasMusic}, HasBibleReading={HasBibleReading}, MusicEnabled={MusicEnabled}",
            model.Id, model.Name, model.Music != null, model.BibleReadingSchedule != null, model.MusicEnabled);

        // Don't pass music if it wasn't updated (for existing schedules)
        if (!IsNewSchedule && !_musicUpdated)
        {
            model.Music = null;
            _logger.Debug("SaveAsync: Music set to null for existing schedule (not updated)");
        }

        _logger.Information("SaveAsync: Calling SaveScheduleAsync. IsNewSchedule={IsNewSchedule}, MusicUpdated={MusicUpdated}, BibleReadingUpdated={BibleReadingUpdated}",
            IsNewSchedule, _musicUpdated, _bibleReadingUpdated);

        var saved = await _schedulePersistenceService.SaveScheduleAsync(model, IsNewSchedule, _musicUpdated, _bibleReadingUpdated);

        if (saved)
        {
            _logger.Information("SaveAsync: Save successful. ScheduleId={ScheduleId}, Model.Id={ModelId}", _scheduleId, model.Id);
            SetupMediaCache(model.Id, isUpdate: !IsNewSchedule);
        }
        else
        {
            _logger.Error("SaveAsync: Save failed. ScheduleId={ScheduleId}", _scheduleId);
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