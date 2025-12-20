using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class ScheduleViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;

    private readonly IToastService popUpService;
    private readonly IMediaCacheSetupService mediaCacheSetupService;
    private readonly INavigationService navigationService;
    private readonly IScheduleDisplayService scheduleDisplayService;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IBibleTranslationService bibleTranslationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly IMapper mapper;

    private int lastScheduleId = -1;
    private bool modelInitialized;
    private bool isInitializingNewSchedule;
    private bool isSaving;
    private AlarmMusic lastMusic;
    private BibleReadingSchedule lastBibleReading;
    private bool isScrolledToBottom;

    public ICommand BatteryOptimizationExcludeCommand { get; private set; }
    public ICommand BatteryOptimizationDismissCommand { get; private set; }

    public ICommand PreviousBookCommand { get; set; }
    public ICommand NextBookCommand { get; set; }

    public ICommand PreviousChapterCommand { get; set; }
    public ICommand NextChapterCommand { get; set; }

    private readonly IScheduleItemStateService scheduleItemStateService;

    public ScheduleViewModel(
        ILogger logger,
        IToastService popUpService,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IBibleNavigationService bibleNavigationService,
        IMediaCacheSetupService mediaCacheSetupService,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IScheduleDisplayService scheduleDisplayService,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IScheduleItemStateService scheduleItemStateService,
        IBibleTranslationService bibleTranslationService,
        IMelodyMusicService melodyMusicService,
        IMapper mapper)
    {
        var constructorStartTime = DateTime.UtcNow;
        this.logger = logger;
        logger.Information("[PERF] ScheduleViewModel: Constructor started at {StartTime}", constructorStartTime);
        this.popUpService = popUpService;
        this.bibleTranslationService = bibleTranslationService;
        this.melodyMusicService = melodyMusicService;
        this.mapper = mapper;

        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.scheduleItemStateService = scheduleItemStateService;

        var bibleNavigationService1 = bibleNavigationService;
        this.mediaCacheSetupService = mediaCacheSetupService;
        this.navigationService = navigationService;
        var scheduleSelectionService1 = scheduleSelectionService;
        this.scheduleDisplayService = scheduleDisplayService;
        this.serviceProvider = serviceProvider;

        state.StateChanged += OnStateChanged;
        state.StateChanged += OnMusicChanged;
        state.StateChanged += OnBibleReadingChanged;

        // Set IsBusy to true by default so the page shows loading indicator immediately
        IsBusy = true;

        // Check if schedule is already in state (e.g., if state changed before this ViewModel was created)
        // This ensures we load the schedule immediately if it's already available
        // Check synchronously first, then also set up a delayed check as fallback
        var checkStateStartTime = DateTime.UtcNow;
        var currentState = state.Value;
        var checkStateElapsed = (DateTime.UtcNow - checkStateStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: State check took {ElapsedMs}ms, CurrentSchedule={HasSchedule}", checkStateElapsed, currentState.CurrentSchedule != null);

        if (currentState.CurrentSchedule != null)
        {
            // Schedule is already in state, trigger the handler immediately on main thread
            logger.Information("[PERF] ScheduleViewModel: Schedule already in state, triggering OnCurrentScheduleChanged");
            MainThread.BeginInvokeOnMainThread(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
        }
        else
        {
            // Schedule not in state yet, set up a delayed check as fallback
            // This handles the case where the action is dispatched but state hasn't updated yet
            logger.Information("[PERF] ScheduleViewModel: Schedule not in state, setting up delayed check");
            _ = Task.Run(async () =>
            {
                // Wait a bit for the state to update after action dispatch
                await Task.Delay(100);
                var delayedState = state.Value;
                if (delayedState.CurrentSchedule != null && !modelInitialized)
                {
                    // Schedule is now in state, trigger the handler
                    logger.Information("[PERF] ScheduleViewModel: Schedule found after delay, triggering OnCurrentScheduleChanged");
                    await MainThread.InvokeOnMainThreadAsync(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
                }
            });
        }

        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: Constructor completed in {ElapsedMs}ms", constructorElapsed);

        // Safety fallback: ensure IsBusy is set to false after a maximum delay
        // This prevents the overlay from staying visible indefinitely if something goes wrong
        _ = Task.Run(async () =>
        {
            // 2 second timeout
            await Task.Delay(2000);
            if (IsBusy && !modelInitialized)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    // Hide Home page overlay as safety fallback
                    dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
                });
            }
        });

        CancelCommand = new AsyncRelayCommand(navigationService.NavigateToHomeAsync);

        SaveCommand = new AsyncRelayCommand(async () =>
        {
            logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
                IsNewSchedule, scheduleId, Name);

            // Set saving flag to prevent ViewModel reset during save
            isSaving = true;

            try
            {
                // Show overlay immediately via state
                dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
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
                {
                    IsEnabled = false;
                }

                if (!IsNewSchedule)
                {
                    if (playbackState.Value.IsPreparingOrPlaying
                        && scheduleId == playbackState.Value.CurrentScheduleId)
                    {
                        await playbackService.StopAsync();
                    }
                }

                var saved = await SaveAsync();

                if (saved)
                {
                    logger.Information("SaveCommand: Save successful, navigating to home. ScheduleId={ScheduleId}", scheduleId);
                    // Wait a bit to ensure the state update from AddScheduleAction has propagated
                    // This ensures the new schedule appears in the Home page list
                    await Task.Delay(100);
                    await navigationService.NavigateToHomeAsync();
                    // Note: Schedule page overlay will be hidden when Home page Appearing event fires
                }
                else
                {
                    logger.Error("SaveCommand: Save failed, hiding overlay. ScheduleId={ScheduleId}", scheduleId);
                    // Hide overlay if save failed
                    dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                }

                if (saved && IsEnabled)
                {
                    await popUpService.ShowScheduledNotification(Model);
                }
            }
            finally
            {
                // Reset saving flag after save completes
                isSaving = false;
            }
        });

        DeleteCommand = new AsyncRelayCommand(async () =>
        {
            // Show overlay immediately via state
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
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
                await navigationService.NavigateToHomeAsync();
                // Note: Schedule page overlay will be hidden when navigating back
                return;
            }

            // For existing schedules, delete and then navigate back
            if (playbackState.Value.IsPreparingOrPlaying
                && scheduleId == playbackState.Value.CurrentScheduleId)
            {
                await playbackService.StopAsync();
            }

            await DeleteAsync();

            await navigationService.NavigateToHomeAsync();
            // Note: Schedule page overlay will be hidden when navigating back
        });

        ToggleDayCommand = new RelayCommand<object>(ToggleDay);

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        SelectMusicCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            Music = await Task.Run(async () =>
                await scheduleSelectionService1.LoadMusicForSelectionAsync(scheduleId, IsNewSchedule, musicUpdated, Music));

            await navigationService.NavigateToMusicSelectionAsync();

            // Map entity to DTO before dispatching
            var musicStateItem = mapper.Map<MusicStateItem>(Music);
            dispatcher.Dispatch(new MusicSelectionAction(musicStateItem));
        });

        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            BibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService1.LoadBibleReadingForSelectionAsync(
                    scheduleId, IsNewSchedule, bibleReadingUpdated, BibleReadingSchedule));

            if (BibleReadingSchedule != null)
            {
                RefreshChapterName();
            }

            await navigationService.NavigateToBibleSelectionAsync();

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = BibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(BibleReadingSchedule)
                : null;
            var tentativeBibleReadingItem = new BibleReadingStateItem
            {
                PublicationCode = BibleReadingSchedule?.PublicationCode ?? "",
                LanguageCode = BibleReadingSchedule?.LanguageCode ?? ""
            };

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BibleSelectionAction(currentBibleReadingItem, tentativeBibleReadingItem));
            }
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.OpenNumberOfChaptersModalAsync(this);
        });

        CloseModalCommand = new AsyncRelayCommand(navigationService.PopModalAsync);

        SelectNumberOfChaptersCommand = new AsyncRelayCommand<NumberOfChaptersListViewItemModel>(async x =>
        {
            if (CurrentNumberOfChapters != null)
            {
                CurrentNumberOfChapters.IsSelected = false;
            }

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

            await navigationService.PopModalAsync();
        });

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        BatteryOptimizationExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    await MarkBatteryOptimizationModalAsShown();
                    await navigationService.PopModalAsync();
                    batteryService.ShowOptimizationSettingsPage();
                }
            }
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();
            await navigationService.PopModalAsync();
        });

        PreviousBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService1.MoveToPreviousBookAsync(BibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        NextBookCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService1.MoveToNextBookAsync(BibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        PreviousChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService1.MoveToPreviousChapterAsync(BibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });

        NextChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (BibleReadingSchedule == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService1.MoveToNextChapterAsync(BibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                RefreshChapterName();
            }
        });
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        var stateValue = state.Value;

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
        logger.Information("[PERF] OnCurrentScheduleChanged: Handler started at {StartTime}", handlerStartTime);

        var stateValue = state.Value;

        // Handle when CurrentSchedule is set (new or existing schedule)
        if (stateValue.CurrentSchedule != null)
        {
            var currentScheduleId = stateValue.CurrentSchedule.Id;
            logger.Information("[PERF] OnCurrentScheduleChanged: Processing schedule Id={ScheduleId}, LastScheduleId={LastScheduleId}, ModelInitialized={ModelInitialized}",
                currentScheduleId, lastScheduleId, modelInitialized);

            if (currentScheduleId == lastScheduleId && modelInitialized)
            {
                // Don't update the model if we're currently saving, as this would overwrite user changes
                if (isSaving)
                {
                    logger.Debug("OnCurrentScheduleChanged: Skipping model update during save operation. ScheduleId={ScheduleId}", currentScheduleId);
                    return;
                }

                // Check if the schedule in Schedules collection has been updated (e.g., from next/prev in home view)
                var updatedScheduleItem = stateValue.Schedules?.FirstOrDefault(s => s.Id == currentScheduleId);
                if (updatedScheduleItem != null)
                {
                    // Map DTO to entity for SetModel
                    var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
                    if (updatedSchedule.Id != Model.Id)
                    {
                        // Schedule was updated externally (e.g., next/prev from home view)
                        // Update the model to reflect the changes
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            SetModel(updatedSchedule);
                            RefreshChapterName();
                        });
                    }
                }
                return;
            }

            isInitializingNewSchedule = false;

            var currentScheduleItem = stateValue.CurrentSchedule;
            lastScheduleId = currentScheduleId;

            var isNew = currentScheduleItem.Id <= 0;
            logger.Information("[PERF] OnCurrentScheduleChanged: IsNew={IsNew}, invoking on main thread", isNew);

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var mainThreadStartTime = DateTime.UtcNow;
                    logger.Information("[PERF] OnCurrentScheduleChanged: Main thread handler started at {StartTime}", mainThreadStartTime);

                    // IsBusy is already true from constructor, no need to set it again
                    IsNewSchedule = isNew;

                    // Map DTO to entity for SetModel
                    var currentSchedule = mapper.Map<AlarmSchedule>(currentScheduleItem);

                    var setModelStartTime = DateTime.UtcNow;
                    SetModel(currentSchedule);
                    var setModelElapsed = (DateTime.UtcNow - setModelStartTime).TotalMilliseconds;
                    logger.Information("[PERF] OnCurrentScheduleChanged: SetModel took {ElapsedMs}ms", setModelElapsed);

                    modelInitialized = true;
                    // Small delay to ensure UI has rendered the content before hiding overlay
                    await Task.Delay(100);
                    IsBusy = false;
                    // Explicitly notify property change to ensure UI updates
                    OnPropertyChanged(nameof(IsBusy));

                    var mainThreadElapsed = (DateTime.UtcNow - mainThreadStartTime).TotalMilliseconds;
                    logger.Information("[PERF] OnCurrentScheduleChanged: Main thread handler completed in {ElapsedMs}ms", mainThreadElapsed);
                    // Note: Home page overlay will be hidden when Schedule page Appearing event fires
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in OnCurrentScheduleChanged handler");
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
            if (modelInitialized && !isSaving)
            {
                logger.Debug("OnCurrentScheduleChanged: Resetting ViewModel for new schedule. Previous ScheduleId={PreviousScheduleId}",
                    scheduleId);
                modelInitialized = false;
                lastScheduleId = -1;
                scheduleId = 0;
                // Will be set to true after initialization
                IsNewSchedule = false;
            }

            if (!modelInitialized && !isInitializingNewSchedule)
            {
                isInitializingNewSchedule = true;
                logger.Information("[PERF] OnCurrentScheduleChanged: Starting new schedule initialization");
                // Show busy indicator during initialization
                MainThread.BeginInvokeOnMainThread(() => IsBusy = true);
                Task.Run(async () =>
                {
                    var getSampleStartTime = DateTime.UtcNow;
                    logger.Information("[PERF] OnCurrentScheduleChanged: GetSampleSchedule started at {StartTime}", getSampleStartTime);

                    var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, bibleTranslationService, melodyMusicService);

                    var getSampleElapsed = (DateTime.UtcNow - getSampleStartTime).TotalMilliseconds;
                    logger.Information("[PERF] OnCurrentScheduleChanged: GetSampleSchedule completed in {ElapsedMs}ms", getSampleElapsed);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var currentState = state.Value;
                        if (isInitializingNewSchedule && !modelInitialized && currentState.CurrentSchedule == null)
                        {
                            logger.Debug("OnCurrentScheduleChanged: Initializing new schedule. SampleSchedule.Id={SampleScheduleId}",
                                sampleSchedule.Id);

                            var setModelStartTime = DateTime.UtcNow;
                            SetModel(sampleSchedule);
                            var setModelElapsed = (DateTime.UtcNow - setModelStartTime).TotalMilliseconds;
                            logger.Information("[PERF] OnCurrentScheduleChanged: SetModel for new schedule took {ElapsedMs}ms", setModelElapsed);

                            modelInitialized = true;
                            IsNewSchedule = true;
                            logger.Information("OnCurrentScheduleChanged: New schedule initialized. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}",
                                scheduleId, IsNewSchedule);
                        }

                        isInitializingNewSchedule = false;
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
        if (Model == null)
        {
            return;
        }

        var stateValue = state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null)
        {
            return;
        }

        // Check if the bible reading actually changed by comparing properties
        var newBibleReadingItem = stateValue.CurrentBibleReadingSchedule;
        var hasChanged = lastBibleReading == null ||
                        BibleReadingSchedule == null ||
                        lastBibleReading.LanguageCode != newBibleReadingItem.LanguageCode ||
                        lastBibleReading.PublicationCode != newBibleReadingItem.PublicationCode ||
                        lastBibleReading.BookNumber != newBibleReadingItem.BookNumber ||
                        lastBibleReading.ChapterNumber != newBibleReadingItem.ChapterNumber ||
                        (BibleReadingSchedule != null &&
                         (BibleReadingSchedule.BookNumber != newBibleReadingItem.BookNumber ||
                          BibleReadingSchedule.ChapterNumber != newBibleReadingItem.ChapterNumber));

        if (!hasChanged)
        {
            return;
        }

        // Map DTO to entity
        var newBibleReading = mapper.Map<BibleReadingSchedule>(newBibleReadingItem);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BibleReadingSchedule = newBibleReading;
            lastBibleReading = newBibleReading;
            bibleReadingUpdated = true;
            RefreshChapterName();
            OnPropertyChanged(nameof(BibleReadingSchedule));
            OnPropertyChanged(nameof(BibleReadingTitleText));
        });
    }

    private void OnMusicChanged(object sender, EventArgs e)
    {
        if (Model == null)
        {
            return;
        }

        var stateValue = state.Value;
        if (stateValue.CurrentMusic == null)
        {
            return;
        }

        // Check if the music actually changed by comparing properties
        var newMusicItem = stateValue.CurrentMusic;
        var hasChanged = lastMusic == null ||
                        Music == null ||
                        lastMusic.LanguageCode != newMusicItem.LanguageCode ||
                        lastMusic.PublicationCode != newMusicItem.PublicationCode ||
                        lastMusic.MusicType != newMusicItem.MusicType ||
                        lastMusic.TrackNumber != newMusicItem.TrackNumber ||
                        (Music != null &&
                         (Music.TrackNumber != newMusicItem.TrackNumber ||
                          Music.MusicType != newMusicItem.MusicType ||
                          Music.LanguageCode != newMusicItem.LanguageCode ||
                          Music.PublicationCode != newMusicItem.PublicationCode));

        if (!hasChanged)
        {
            return;
        }

        // Map DTO to entity
        var newMusic = mapper.Map<AlarmMusic>(newMusicItem);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Music = newMusic;
            lastMusic = newMusic;
            musicUpdated = true;
            OnPropertyChanged(nameof(Music));
        });
    }

    private bool canOptimizeBattery;

    public bool CanOptimizeBattery
    {
        get => canOptimizeBattery;
        set => SetProperty(ref canOptimizeBattery, value);
    }

    private async Task MarkBatteryOptimizationModalAsShown()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                await batteryService.MarkModalAsShownAsync();
            }
        }
    }

    private async Task ShowBatteryOptimizationExclusionPage()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
        if (batteryService == null)
        {
            return;
        }

        if (batteryService.CanShowOptimizeActivity())
        {
            CanOptimizeBattery = true;
        }

        if (await batteryService.ShouldShowModalAsync())
        {
            await navigationService.OpenBatteryOptimizationModalAsync(this);
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

    private ObservableCollection<NumberOfChaptersListViewItemModel> numberOfChaptersList;

    public ObservableCollection<NumberOfChaptersListViewItemModel> NumberOfChaptersList
    {
        get => numberOfChaptersList;
        set => SetProperty(ref numberOfChaptersList, value);
    }

    private NumberOfChaptersListViewItemModel currentNumberOfChapters;

    public NumberOfChaptersListViewItemModel CurrentNumberOfChapters
    {
        get => currentNumberOfChapters;
        set
        {
            if (SetProperty(ref currentNumberOfChapters, value))
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
        logger.Debug("GetModel: Reading ViewModel properties. ViewModel.Name={ViewModelName}, ViewModel.MusicEnabled={ViewModelMusicEnabled}, ViewModel.IsEnabled={ViewModelIsEnabled}",
            Name, MusicEnabled, IsEnabled);

        Model.Id = scheduleId;

        Model.Name = Name;
        Model.IsEnabled = IsEnabled;
        Model.DaysOfWeek = DaysOfWeek;
        Model.Hour = Time.Hours;
        Model.Minute = Time.Minutes;
        Model.MusicEnabled = MusicEnabled;
        Model.NotificationEnabled = notificationEnabled;
        Model.AlwaysPlayFromStart = AlwaysPlayFromStart;

        // Use CurrentNumberOfChapters.Value if available, otherwise keep the Model's existing value
        // This ensures we always save the currently selected value, even if SetModel() was called and reset it
        if (CurrentNumberOfChapters != null)
        {
            var newValue = CurrentNumberOfChapters.Value;
            Model.NumberOfChaptersToRead = newValue;
            logger.Debug("GetModel: Setting NumberOfChaptersToRead from CurrentNumberOfChapters.Value = {Value} (Model had {OldValue})",
                newValue, Model.NumberOfChaptersToRead);
        }
        else
        {
            logger.Warning("GetModel: CurrentNumberOfChapters is null, keeping existing Model.NumberOfChaptersToRead = {Value}", Model.NumberOfChaptersToRead);
        }

        logger.Debug("GetModel: After setting Model properties. Model.Id={ModelId}, Model.Name={ModelName}, Model.MusicEnabled={ModelMusicEnabled}, Model.IsEnabled={ModelIsEnabled}, Model.DaysOfWeek={ModelDaysOfWeek}",
            Model.Id, Model.Name, Model.MusicEnabled, Model.IsEnabled, Model.DaysOfWeek);

        return Model;
    }

    private void SetModel(AlarmSchedule model)
    {
        Model = model.DeepClone();

        scheduleId = model.Id;
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

    private int scheduleId;

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }


    private string name;

    public string Name
    {
        get => name;
        set
        {
            logger.Debug("Name: Setting value from '{OldValue}' to '{NewValue}'", name, value);
            SetProperty(ref name, value);
        }
    }

    private bool isEnabled;

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    private DaysOfWeek daysOfWeek;

    public DaysOfWeek DaysOfWeek
    {
        get => daysOfWeek;
        set => SetProperty(ref daysOfWeek, value);
    }

    private TimeSpan time;

    public TimeSpan Time
    {
        get => time;
        set => SetProperty(ref time, value);
    }

    public string Hour => (Time.Hours % 12).ToString("D2");

    public string Minute => Time.Minutes.ToString("D2");

    public Meridian Meridian => Time.Hours < 12 ? Meridian.Am : Meridian.Pm;

    private bool musicEnabled;

    public bool MusicEnabled
    {
        get => musicEnabled;
        set
        {
            logger.Debug("MusicEnabled: Setting value from {OldValue} to {NewValue}", musicEnabled, value);
            SetProperty(ref musicEnabled, value);
        }
    }

    private bool notificationEnabled;

    public bool NotificationEnabled
    {
        get => notificationEnabled;
        set
        {
            if (!value)
            {
                _ = ShowBatteryOptimizationExclusionPage();
            }

            SetProperty(ref notificationEnabled, value);
        }
    }

    private bool alwaysPlayFromStart;

    public bool AlwaysPlayFromStart
    {
        get => alwaysPlayFromStart;
        set => SetProperty(ref alwaysPlayFromStart, value);
    }

    private bool musicUpdated;

    public AlarmMusic Music
    {
        get => Model.Music;
        set => Model.Music = value;
    }

    private bool bibleReadingUpdated;

    public BibleReadingSchedule BibleReadingSchedule
    {
        get => Model.BibleReadingSchedule;
        set => Model.BibleReadingSchedule = value;
    }

    private string bibleReadingTitleText;

    public string BibleReadingTitleText
    {
        get => bibleReadingTitleText;
        set => SetProperty(ref bibleReadingTitleText, value);
    }

    private bool isNewSchedule;
    private bool isExistingSchedule;

    public bool IsNewSchedule
    {
        get => isNewSchedule;
        private set
        {
            IsExistingSchedule = !value;
            SetProperty(ref isNewSchedule, value);
        }
    }

    public bool IsScrolledToBottom
    {
        get => isScrolledToBottom;
        set => SetProperty(ref isScrolledToBottom, value);
    }

    public bool IsExistingSchedule
    {
        get => isExistingSchedule;
        private set => SetProperty(ref isExistingSchedule, value);
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
        {
            DaysOfWeek &= ~day;
        }
        else
        {
            DaysOfWeek |= day;
        }

        OnPropertyChanged(nameof(DaysOfWeek));
    }

    private void SetupMediaCache(int scheduleId, bool isUpdate = false)
    {
        if (scheduleId <= 0)
        {
            logger.Warning("Skipping media cache setup for invalid schedule ID: {ScheduleId}", scheduleId);
            return;
        }

        if (isUpdate)
        {
            // For updates, delete old cache files first in a separate task, then cache new files
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
                    await mediaCacheService.DeleteScheduleCacheAsync(scheduleId);
                    await mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error deleting old cache and setting up new cache for schedule {ScheduleId}", scheduleId);
                }
            });
        }
        else
        {
            // For new schedules, just cache files
            _ = mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
        }
    }

    private async Task<bool> SaveAsync()
    {
        logger.Information("SaveAsync: Starting save. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}, ModelInitialized={ModelInitialized}",
            IsNewSchedule, scheduleId, Name, modelInitialized);

        if (!modelInitialized)
        {
            logger.Error("SaveAsync: Model not initialized. Cannot save.");
            await popUpService.ShowMessage("Schedule data is not ready. Please try again.");
            return false;
        }

        if (Model == null)
        {
            logger.Error("SaveAsync: Model is null. Cannot save.");
            await popUpService.ShowMessage("Schedule data is missing. Please try again.");
            return false;
        }

        if (!IsNewSchedule && scheduleId <= 0)
        {
            logger.Error("SaveAsync: Invalid ScheduleId for existing schedule. ScheduleId={ScheduleId}", scheduleId);
            await popUpService.ShowMessage("Invalid schedule ID. Please try again.");
            return false;
        }

        if (!await Validate())
        {
            logger.Warning("SaveAsync: Validation failed");
            return false;
        }

        if (IsNewSchedule)
        {
            IsEnabled = true;
        }

        var model = GetModel();

        // Ensure BibleReadingSchedule uses "nwt" (2013) as default if publication code is empty or invalid
        if (model.BibleReadingSchedule != null && string.IsNullOrWhiteSpace(model.BibleReadingSchedule.PublicationCode))
        {
            logger.Warning("SaveAsync: BibleReadingSchedule has empty PublicationCode, defaulting to 'nwt' (2013)");
            model.BibleReadingSchedule.PublicationCode = "nwt";
        }

        logger.Debug("SaveAsync: Model retrieved. Model.Id={ModelId}, Model.Name={ModelName}, HasMusic={HasMusic}, HasBibleReading={HasBibleReading}, MusicEnabled={MusicEnabled}, PublicationCode={PublicationCode}",
            model.Id, model.Name, model.Music != null, model.BibleReadingSchedule != null, model.MusicEnabled,
            model.BibleReadingSchedule?.PublicationCode ?? "null");

        // Don't pass music if it wasn't updated (for existing schedules)
        if (!IsNewSchedule && !musicUpdated)
        {
            model.Music = null;
            logger.Debug("SaveAsync: Music set to null for existing schedule (not updated)");
        }

        logger.Information("SaveAsync: Mapping AlarmSchedule to ScheduleStateItem and dispatching action. IsNewSchedule={IsNewSchedule}, MusicUpdated={MusicUpdated}, BibleReadingUpdated={BibleReadingUpdated}",
            IsNewSchedule, musicUpdated, bibleReadingUpdated);

        // Map DB entity (AlarmSchedule) → domain model (ScheduleStateItem)
        var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);

        // Dispatch action with domain model (following Fluxor best practices)
        if (IsNewSchedule)
        {
            logger.Information("SaveAsync: Dispatching CreateScheduleAction");
            dispatcher.Dispatch(new CreateScheduleAction(scheduleStateItem, musicUpdated, bibleReadingUpdated));
        }
        else
        {
            logger.Information("SaveAsync: Dispatching UpdateScheduleFromViewModelAction");
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, musicUpdated, bibleReadingUpdated));
        }

        // Setup media cache optimistically (will be set up even if save fails later)
        // In production, you might want to wait for success action before calling this
        SetupMediaCache(model.Id, isUpdate: !IsNewSchedule);

        // Return true optimistically - the Effect will handle the actual save and dispatch success/failure
        return true;
    }

    private async Task<bool> Validate()
    {
        if (DaysOfWeek != 0)
        {
            return true;
        }

        await popUpService.ShowMessage("Please select day(s) of Week.");
        return false;

    }

    private async Task DeleteAsync()
    {
        if (scheduleId <= 0)
        {
            return;
        }

        // Check if this is the last schedule - prevent deletion if it is
        var scheduleCount = state.Value.Schedules?.Count ?? 0;
        if (scheduleCount <= 1)
        {
            logger.Warning("Cannot delete schedule {ScheduleId} - it is the last schedule", scheduleId);
            await popUpService.ShowMessage("Cannot delete last schedule");
            return;
        }

        logger.Information("DeleteAsync: Dispatching DeleteScheduleAction for ScheduleId={ScheduleId}", scheduleId);

        // Dispatch action with schedule ID (following Fluxor best practices)
        dispatcher.Dispatch(new DeleteScheduleAction(scheduleId));

        // Note: The Effect will handle the actual deletion and dispatch success/failure
        // Media cache deletion is handled in the Effect
    }

    private void RefreshChapterName()
    {
        if (BibleReadingSchedule == null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var displayName = await scheduleDisplayService.GetChapterDisplayNameForBibleReadingAsync(
                    scheduleId, BibleReadingSchedule, true);

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
                logger.Error(e, "An error happened while refreshing chapter name for schedule {ScheduleId}", scheduleId);
            }
        });
    }

    /// <summary>
    /// Hides the Home page overlay. Called when the Schedule page is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay() => dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// This property is bound to the Schedule page overlay.
    /// </summary>
    public bool IsSchedulePageOverlayVisible => state.Value.IsSchedulePageOverlayVisible;

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        state.StateChanged -= OnMusicChanged;
        state.StateChanged -= OnBibleReadingChanged;
    }
}
