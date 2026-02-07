#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class ScheduleViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Helper classes
    private readonly ScheduleStateManager stateManager;
    private readonly ScheduleCommandExecutor commandExecutor;
    private readonly SchedulePropertyManager propertyManager;
    private readonly ScheduleContainerManager containerManager;
    private readonly ScheduleOverlayManager overlayManager;
    private readonly ScheduleOverlayTimeoutController overlayTimeoutController;

    // Track last category name to detect changes
    private string? lastCategoryName;


    public ScheduleViewModel(
        ILogger logger,
        IToastService popUpService,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IMediaCacheSetupService mediaCacheSetupService,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IBiblePublicationService BiblePublicationService,
        IMelodyMusicService melodyMusicService,
        IMediaService mediaService,
        IMapper mapper,
        IAlarmScheduleService alarmScheduleService,
        IScheduleDisplayNameService scheduleDisplayNameService,
        IScheduleSaveService scheduleSaveService,
        IScheduleValidationService scheduleValidationService,
        IScheduleInitializationService scheduleInitializationService,
        IScheduleCommandService scheduleCommandService,
        IScheduleMediaCacheService scheduleMediaCacheService,
        IScheduleContainerService scheduleContainerService,
        ScheduleStateChangeHandler scheduleStateChangeHandler)
    {
#if DEBUG
        var constructorStartTime = DateTime.UtcNow;
        Log.Logger.Information("[PERF] ScheduleViewModel: Constructor started at {StartTime}", constructorStartTime);
#endif

        // Initialize readonly fields
        this.logger = logger;
        this.mapper = mapper;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.serviceProvider = serviceProvider;

        // Initialize helper classes
        stateManager = new ScheduleStateManager(scheduleInitializationService, scheduleStateChangeHandler, dispatcher, logger);
        propertyManager = new SchedulePropertyManager(state, logger);
        commandExecutor = new ScheduleCommandExecutor(
            scheduleCommandService, 
            scheduleMediaCacheService, 
            state, 
            playbackState, 
            dispatcher, 
            mapper, 
            logger, 
            () => propertyManager?.MusicSelectionContainerViewModel,
            () => propertyManager?.AlarmSettingsContainerViewModel,
            () => propertyManager?.NumberOfTrackContainerViewModel,
            SetIsSaving,
            (isBusy) => propertyManager.IsCancelBusy = isBusy,
            (isBusy) => propertyManager.IsSaveBusy = isBusy,
            (isBusy) => propertyManager.IsDeleteBusy = isBusy);
        containerManager = new ScheduleContainerManager(scheduleContainerService, serviceProvider);
        overlayManager = new ScheduleOverlayManager(dispatcher);
        overlayTimeoutController = new ScheduleOverlayTimeoutController(logger, state, dispatcher);

        // Subscribe to property manager changes to forward property changes
        propertyManager.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SchedulePropertyManager.IsSchedulePageOverlayVisible))
            {
                OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
            }
            // Forward IsExistingSchedule property changes (for Delete button visibility)
            else if (e.PropertyName == nameof(SchedulePropertyManager.IsExistingSchedule))
            {
                OnPropertyChanged(nameof(IsExistingSchedule));
            }
            // Forward IsNewSchedule property changes
            else if (e.PropertyName == nameof(SchedulePropertyManager.IsNewSchedule))
            {
                OnPropertyChanged(nameof(IsNewSchedule));
            }
            // Forward busy property changes
            else if (e.PropertyName == nameof(SchedulePropertyManager.IsCancelBusy))
            {
                OnPropertyChanged(nameof(IsCancelBusy));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.IsSaveBusy))
            {
                OnPropertyChanged(nameof(IsSaveBusy));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.IsDeleteBusy))
            {
                OnPropertyChanged(nameof(IsDeleteBusy));
            }
            // Forward container ViewModel property changes
            else if (e.PropertyName == nameof(SchedulePropertyManager.BibleSelectionContainerViewModel))
            {
                OnPropertyChanged(nameof(BibleSelectionContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.MusicSelectionContainerViewModel))
            {
                OnPropertyChanged(nameof(MusicSelectionContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.NumberOfTrackContainerViewModel))
            {
                OnPropertyChanged(nameof(NumberOfTrackContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.ScheduleDetailsContainerViewModel))
            {
                OnPropertyChanged(nameof(ScheduleDetailsContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.AlarmSettingsContainerViewModel))
            {
                OnPropertyChanged(nameof(AlarmSettingsContainerViewModel));
            }
        };

        // Subscribe to state changes
        state.StateChanged += OnStateChanged;

        // Initialize lastCategoryName from current state
        var initialSchedule = state.Value.CurrentSchedule;
        if (initialSchedule != null)
        {
            lastCategoryName = initialSchedule.BiblePublicationCategoryName;
            logger.Debug("ScheduleViewModel: Initialized lastCategoryName to '{Category}' from initial schedule",
                lastCategoryName ?? "(null)");
        }

        // Initialize IsNewSchedule immediately from current state
        // This ensures the Delete button visibility is correct from the start
        var initialIsNew = initialSchedule == null || initialSchedule.Id <= 0;
        propertyManager.IsNewSchedule = initialIsNew;

        // Initialize state handling and commands
        stateManager.InitializeStateHandling(
            state,
            () => propertyManager.IsBusy = true,
            () => overlayManager.ShowSchedulePageOverlay());
        commandExecutor.InitializeCommands(out var cancelCmd, out var saveCmd, out var deleteCmd);
        CancelCommand = cancelCmd;
        SaveCommand = saveCmd;
        DeleteCommand = deleteCmd;

        // Initialize containers asynchronously after page is visible
        _ = InitializeContainerViewModelsAsync();

#if DEBUG
        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: Constructor completed in {ElapsedMs}ms", constructorElapsed);
#endif
    }

    private bool isInitializingContainers;
    private bool hasInitializedContainersOnce;

    /// <summary>
    /// Called when the page reappears (e.g., navigating back to it or on device where page is reused).
    /// Re-initializes containers to ensure fresh state on each visit.
    /// This is critical on physical devices where page instances may be cached/reused.
    /// </summary>
    public void OnPageReappearing()
    {
        // Skip if this is the first time (constructor already started initialization)
        // Only reinitialize on subsequent visits
        if (!hasInitializedContainersOnce)
        {
            return;
        }

        _ = InitializeContainerViewModelsAsync();
    }

    /// <summary>
    /// Initializes container view models asynchronously off the UI thread.
    /// This prevents blocking the UI thread during page load.
    /// Containers are created in Task.Run, then assigned on the UI thread.
    /// Always creates fresh container instances - disposes any existing containers first.
    /// Since containers are registered as Transient, GetRequiredService will create fresh instances.
    /// </summary>
    private async Task InitializeContainerViewModelsAsync()
    {
        // Prevent concurrent initialization
        if (isInitializingContainers)
        {
            return;
        }

        isInitializingContainers = true;

        try
        {
            // Dispose any existing containers first to ensure clean state
            // This is important when page/ViewModel is reused on device
            containerManager.DisposeContainers(propertyManager);

            await containerManager.InitializeContainerViewModelsAsync((bible, music, tracks, details, alarmSettings) =>
            {
                propertyManager.BibleSelectionContainerViewModel = bible;
                propertyManager.MusicSelectionContainerViewModel = music;
                propertyManager.NumberOfTrackContainerViewModel = tracks;
                propertyManager.ScheduleDetailsContainerViewModel = details;
                propertyManager.AlarmSettingsContainerViewModel = alarmSettings;
            });

            hasInitializedContainersOnce = true;
        }
        finally
        {
            isInitializingContainers = false;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;

        // Marshal property updates to UI thread to ensure PropertyChanged events are raised on the correct thread
        // This is important because OnStateChanged can be called from background threads when Fluxor actions
        // are dispatched from Task.Run or other background operations (e.g., InitializeNewScheduleAsync)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Sync overlay visibility with state
            propertyManager.IsSchedulePageOverlayVisible = stateValue.IsSchedulePageOverlayVisible;

            // Update IsNewSchedule based on CurrentSchedule ID
            // This determines if the delete button should be visible
            // Always set it (not just when changed) to ensure it's initialized correctly
            var currentSchedule = stateValue.CurrentSchedule;
            var isNew = currentSchedule == null || currentSchedule.Id <= 0;
            propertyManager.IsNewSchedule = isNew;

            // Notify UI of property changes when CurrentSchedule changes
            // Note: NotifySchedulePropertiesChanged also marshals to UI thread, but since we're already
            // on UI thread here, it will execute immediately (MainThread.BeginInvokeOnMainThread checks
            // if already on main thread and executes synchronously if so)
            propertyManager.NotifySchedulePropertiesChanged();
            
            // Always notify IsMusicSelectionVisible when schedule changes, since it's computed from schedule state
            // Check if category changed to track for logging
            var currentCategoryName = currentSchedule?.BiblePublicationCategoryName;
            var categoryChanged = currentCategoryName != lastCategoryName;
            
            if (categoryChanged)
            {
                logger.Debug("ScheduleViewModel.OnStateChanged: Category changed from '{LastCategory}' to '{CurrentCategory}', notifying IsMusicSelectionVisible",
                    lastCategoryName ?? "(null)", currentCategoryName ?? "(null)");
                lastCategoryName = currentCategoryName;
            }
            else if (lastCategoryName == null && currentCategoryName != null)
            {
                // Initialize lastCategoryName on first load
                logger.Debug("ScheduleViewModel.OnStateChanged: Initializing lastCategoryName to '{Category}'",
                    currentCategoryName);
                lastCategoryName = currentCategoryName;
            }
            
            // Always raise property change for IsMusicSelectionVisible when schedule changes
            // This ensures the binding is updated even if the category name didn't change but other schedule properties did
            OnPropertyChanged(nameof(IsMusicSelectionVisible));
            
            // Log the computed visibility value
            var isVisible = IsMusicSelectionVisible;
            if (categoryChanged || lastCategoryName == null)
            {
                logger.Debug("ScheduleViewModel.OnStateChanged: IsMusicSelectionVisible={IsVisible} for category='{Category}'",
                    isVisible, currentCategoryName ?? "(null)");
            }
        });

        overlayTimeoutController.HandleStateChanged(stateValue);
    }





    public ICommand CancelCommand { get; set; } = null!;

    public ICommand SaveCommand { get; set; } = null!;
    public ICommand DeleteCommand { get; set; } = null!;


    private int ScheduleId => SchedulePropertyHelper.GetScheduleId(state.Value.CurrentSchedule);

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public bool IsCancelBusy
    {
        get => propertyManager.IsCancelBusy;
        set => propertyManager.IsCancelBusy = value;
    }

    public bool IsSaveBusy
    {
        get => propertyManager.IsSaveBusy;
        set => propertyManager.IsSaveBusy = value;
    }

    public bool IsDeleteBusy
    {
        get => propertyManager.IsDeleteBusy;
        set => propertyManager.IsDeleteBusy = value;
    }


    public string Name => SchedulePropertyHelper.GetName(state.Value.CurrentSchedule);

    public bool IsEnabled => SchedulePropertyHelper.GetIsEnabled(state.Value.CurrentSchedule);

    public DaysOfWeek DaysOfWeek => SchedulePropertyHelper.GetDaysOfWeek(state.Value.CurrentSchedule);

    public TimeSpan Time => SchedulePropertyHelper.GetTime(state.Value.CurrentSchedule);

    public bool MusicEnabled => SchedulePropertyHelper.GetMusicEnabled(state.Value.CurrentSchedule);

    public AlarmMusic? Music
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return null;
            }
            return mapper.Map<AlarmSchedule>(currentSchedule).Music;
        }
        set
        {
            // Music updates are handled by MusicSelectionContainerViewModel via actions
            // This setter is kept for backward compatibility but doesn't need to do anything
            // as the container view model dispatches actions directly
        }
    }

    public BiblePublicationSchedule? BiblePublicationSchedule
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return null;
            }
            return mapper.Map<AlarmSchedule>(currentSchedule).BiblePublicationSchedule;
        }
        set
        {
            // Bible reading updates are handled by BibleSelectionContainerViewModel via actions
            // This setter is kept for backward compatibility but doesn't need to do anything
            // as the container view model dispatches actions directly
        }
    }

    public bool IsNewSchedule
    {
        get => propertyManager.IsNewSchedule;
        set => propertyManager.IsNewSchedule = value;
    }

    public bool IsScrolledToBottom
    {
        get => propertyManager.IsScrolledToBottom;
        set => propertyManager.IsScrolledToBottom = value;
    }

    public bool IsExistingSchedule => propertyManager.IsExistingSchedule;

    /// <summary>
    /// Determines if MusicSelectionContainer should be visible.
    /// Returns false when Music category is selected (Music category publications don't use the music selection container).
    /// </summary>
    public bool IsMusicSelectionVisible
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Debug("IsMusicSelectionVisible: CurrentSchedule is null, returning false (hide container)");
                return false; // Hide container when no schedule is active (e.g., during navigation)
            }

            var categoryName = currentSchedule.BiblePublicationCategoryName;
            var isMusicCategory = !string.IsNullOrWhiteSpace(categoryName) &&
                                  string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase);
            var isVisible = !isMusicCategory;
            
            logger.Debug("IsMusicSelectionVisible: Category='{Category}', IsMusicCategory={IsMusicCategory}, Returning={IsVisible}",
                categoryName ?? "(null)", isMusicCategory, isVisible);
            
            return isVisible;
        }
    }

    // Container ViewModels - exposed for XAML binding
    public BiblePublicationSelectionContainerViewModel? BibleSelectionContainerViewModel => propertyManager.BibleSelectionContainerViewModel;
    public MusicSelectionContainerViewModel? MusicSelectionContainerViewModel => propertyManager.MusicSelectionContainerViewModel;
    public NumberOfTrackContainerViewModel? NumberOfTrackContainerViewModel => propertyManager.NumberOfTrackContainerViewModel;
    public ScheduleDetailsContainerViewModel? ScheduleDetailsContainerViewModel => propertyManager.ScheduleDetailsContainerViewModel;
    public AlarmSettingsContainerViewModel? AlarmSettingsContainerViewModel => propertyManager.AlarmSettingsContainerViewModel;

    /// <summary>
    /// Hides the Home page overlay. Called when the Schedule page is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay() => overlayManager.HideHomePageOverlay();

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// This property is bound to the Schedule page overlay.
    /// Uses a cached value that's updated when state changes to ensure bindings work correctly.
    /// </summary>
    public bool IsSchedulePageOverlayVisible => propertyManager.IsSchedulePageOverlayVisible;

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => overlayManager.HideSchedulePageOverlay();

    /// <summary>
    /// Checks if all containers are ready. Used by page code-behind to determine when to hide overlay.
    /// </summary>
    public bool AreAllContainersReady => state.Value.ContainerReadiness.AllReady;

    /// <summary>
    /// Resets the content loaded flag. Called when page appears to handle re-navigation.
    /// </summary>
    public void ResetContentLoaded()
    {
        overlayTimeoutController.ResetContentLoaded();
    }

    /// <summary>
    /// Called by page code-behind after content is loaded.
    /// Hides overlay if all containers are ready.
    /// </summary>
    public void OnContentLoaded()
    {
        overlayTimeoutController.OnContentLoaded();
    }

    /// <summary>
    /// Sets the saving flag to prevent OnContentLoaded from hiding the overlay during save operations.
    /// </summary>
    public void SetIsSaving(bool saving)
    {
        overlayTimeoutController.SetIsSaving(saving);
    }

    public void StopPermissionCheckTasks()
    {
#if ANDROID
        propertyManager.AlarmSettingsContainerViewModel?.StopPermissionCheckTaskIfRunning();
        propertyManager.NumberOfTrackContainerViewModel?.StopPermissionCheckTaskIfRunning();
#endif
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        overlayTimeoutController.Dispose();
        containerManager.DisposeContainers(propertyManager);
        overlayManager.Dispose();
    }
}

