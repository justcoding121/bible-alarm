#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed partial class ScheduleViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Helper classes
    private readonly ScheduleContainerManager containerManager;
    private readonly ScheduleOverlayManager overlayManager;
    private readonly ScheduleOverlayTimeoutController overlayTimeoutController;

    private BiblePublicationSelectionContainerViewModel? bibleSelectionContainerViewModel;
    private MusicSelectionContainerViewModel? musicSelectionContainerViewModel;
    private NumberOfTrackContainerViewModel? numberOfTrackContainerViewModel;
    private ScheduleDetailsContainerViewModel? scheduleDetailsContainerViewModel;
    private AlarmSettingsContainerViewModel? alarmSettingsContainerViewModel;

    private bool isBusy;
    private bool isNewSchedule;
    private bool isExistingSchedule;
    private bool isScrolledToBottom;
    private bool isSchedulePageOverlayVisible = true;
    private bool isCancelBusy;
    private bool isSaveBusy;
    private bool isDeleteBusy;

    // Track last category name to detect changes
    private string? lastCategoryName;

    private const string LogNullDisplayLabel = "(null)";


    public ScheduleViewModel(ScheduleViewModelDeps deps)
    {
#if DEBUG
        var constructorStartTime = DateTime.UtcNow;
        Log.Logger.Information("[PERF] ScheduleViewModel: Constructor started at {StartTime}", constructorStartTime);
#endif

        logger = deps.Logger;
        mapper = deps.Mapper;
        state = deps.ApplicationState;
        playbackState = deps.PlaybackState;
        dispatcher = deps.Dispatcher;
        serviceProvider = deps.ServiceProvider;

        var stateManager = new ScheduleStateManager(deps.ScheduleInitializationService, dispatcher, logger);
        var commandExecutor = new ScheduleCommandExecutor(
            new ScheduleCommandExecutorCoreDeps(
                deps.ScheduleCommandService,
                deps.ScheduleMediaCacheService,
                state,
                playbackState,
                dispatcher,
                mapper,
                logger),
            new ScheduleCommandExecutorUiHooks(
                () => MusicSelectionContainerViewModel,
                () => AlarmSettingsContainerViewModel,
                () => NumberOfTrackContainerViewModel,
                SetIsSaving,
                busy => IsCancelBusy = busy,
                busy => IsSaveBusy = busy,
                busy => IsDeleteBusy = busy));
        containerManager = new ScheduleContainerManager(deps.ScheduleContainerService, serviceProvider);
        overlayManager = new ScheduleOverlayManager(this.dispatcher);
        overlayTimeoutController = new ScheduleOverlayTimeoutController(logger, state, this.dispatcher);

        // Subscribe to state changes
        state.StateChanged += OnStateChanged;

        // Initialize lastCategoryName from current state
        var initialSchedule = state.Value.CurrentSchedule;
        if (initialSchedule != null)
        {
            lastCategoryName = initialSchedule.BiblePublicationCategoryName;
            logger.Debug("ScheduleViewModel: Initialized lastCategoryName to '{Category}' from initial schedule",
                lastCategoryName ?? LogNullDisplayLabel);
        }

        // Initialize IsNewSchedule immediately from current state
        // This ensures the Delete button visibility is correct from the start
        var initialIsNew = initialSchedule == null || initialSchedule.Id <= 0;
        IsNewSchedule = initialIsNew;

        // Initialize state handling and commands
        stateManager.InitializeStateHandling(
            state,
            () => IsBusy = true,
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
            DisposeContainers();

            await containerManager.InitializeContainerViewModelsAsync((bible, music, tracks, details, alarmSettings) =>
            {
                BibleSelectionContainerViewModel = bible;
                MusicSelectionContainerViewModel = music;
                NumberOfTrackContainerViewModel = tracks;
                ScheduleDetailsContainerViewModel = details;
                AlarmSettingsContainerViewModel = alarmSettings;
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
            IsSchedulePageOverlayVisible = stateValue.IsSchedulePageOverlayVisible;

            // Update IsNewSchedule based on CurrentSchedule ID
            // This determines if the delete button should be visible
            // Always set it (not just when changed) to ensure it's initialized correctly
            var currentSchedule = stateValue.CurrentSchedule;
            var isNew = currentSchedule == null || currentSchedule.Id <= 0;
            IsNewSchedule = isNew;

            // Notify UI of property changes when CurrentSchedule changes
            // Note: NotifySchedulePropertiesChanged also marshals to UI thread, but since we're already
            // on UI thread here, it will execute immediately (MainThread.BeginInvokeOnMainThread checks
            // if already on main thread and executes synchronously if so)
            NotifySchedulePropertiesChanged();
            
            // Always notify IsMusicSelectionVisible when schedule changes, since it's computed from schedule state
            // Check if category changed to track for logging
            var currentCategoryName = currentSchedule?.BiblePublicationCategoryName;
            var categoryChanged = currentCategoryName != lastCategoryName;
            
            if (categoryChanged)
            {
                logger.Debug("ScheduleViewModel.OnStateChanged: Category changed from '{LastCategory}' to '{CurrentCategory}', notifying IsMusicSelectionVisible",
                    lastCategoryName ?? LogNullDisplayLabel, currentCategoryName ?? LogNullDisplayLabel);
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
                    isVisible, currentCategoryName ?? LogNullDisplayLabel);
            }
        });

        overlayTimeoutController.HandleStateChanged(stateValue);
    }





    public ICommand CancelCommand { get; set; }

    public ICommand SaveCommand { get; set; }
    public ICommand DeleteCommand { get; set; }

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    public bool IsSaveBusy
    {
        get => isSaveBusy;
        set => SetProperty(ref isSaveBusy, value);
    }

    public bool IsDeleteBusy
    {
        get => isDeleteBusy;
        set => SetProperty(ref isDeleteBusy, value);
    }


    public string Name => SchedulePropertyHelper.GetName(state.Value.CurrentSchedule);

    public bool IsEnabled => SchedulePropertyHelper.GetIsEnabled(state.Value.CurrentSchedule);

    public WeekDays DaysOfWeek => SchedulePropertyHelper.GetDaysOfWeek(state.Value.CurrentSchedule);

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
            // This setter is kept for backward compatibility; value is intentionally not used
            _ = value;
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
            // This setter is kept for backward compatibility; value is intentionally not used
            _ = value;
        }
    }

    public bool IsNewSchedule
    {
        get => isNewSchedule;
        set
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

    /// <summary>
    /// Determines if MusicSelectionContainer should be visible.
    /// Returns false when the selected publication is a music publication (BiblePublicationIsMusic or publication code in MusicFlagPublicationCodes).
    /// </summary>
    public bool IsMusicSelectionVisible
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Debug("IsMusicSelectionVisible: CurrentSchedule is null, returning false (hide container)");
                return false;
            }

            var pubCode = currentSchedule.BiblePublicationCode;
            var isMusicPublication = currentSchedule.BiblePublicationIsMusic ||
                (!string.IsNullOrWhiteSpace(pubCode) && JwSourceHelper.MusicFlagPublicationCodes.Contains(pubCode));
            var isVisible = !isMusicPublication;
            logger.Debug("IsMusicSelectionVisible: BiblePublicationIsMusic={IsMusic}, PubCode={PubCode}, IsMusicPublication={IsMusicPub}, Returning={IsVisible}",
                currentSchedule.BiblePublicationIsMusic, pubCode ?? LogNullDisplayLabel, isMusicPublication, isVisible);
            return isVisible;
        }
    }

    // Container ViewModels - exposed for XAML binding
    public BiblePublicationSelectionContainerViewModel? BibleSelectionContainerViewModel
    {
        get => bibleSelectionContainerViewModel;
        private set => SetProperty(ref bibleSelectionContainerViewModel, value);
    }

    public MusicSelectionContainerViewModel? MusicSelectionContainerViewModel
    {
        get => musicSelectionContainerViewModel;
        private set => SetProperty(ref musicSelectionContainerViewModel, value);
    }

    public NumberOfTrackContainerViewModel? NumberOfTrackContainerViewModel
    {
        get => numberOfTrackContainerViewModel;
        private set => SetProperty(ref numberOfTrackContainerViewModel, value);
    }

    public ScheduleDetailsContainerViewModel? ScheduleDetailsContainerViewModel
    {
        get => scheduleDetailsContainerViewModel;
        private set => SetProperty(ref scheduleDetailsContainerViewModel, value);
    }

    public AlarmSettingsContainerViewModel? AlarmSettingsContainerViewModel
    {
        get => alarmSettingsContainerViewModel;
        private set => SetProperty(ref alarmSettingsContainerViewModel, value);
    }

    /// <summary>
    /// Hides the Home page overlay. Called when the Schedule page is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay() => overlayManager.HideHomePageOverlay();

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// This property is bound to the Schedule page overlay.
    /// Uses a cached value that's updated when state changes to ensure bindings work correctly.
    /// </summary>
    public bool IsSchedulePageOverlayVisible
    {
        get => isSchedulePageOverlayVisible;
        private set
        {
            // Prevent setting the same value repeatedly to avoid infinite loops
            if (isSchedulePageOverlayVisible == value)
            {
                return;
            }

            if (SetProperty(ref isSchedulePageOverlayVisible, value))
            {
                logger.Debug("IsSchedulePageOverlayVisible: Property changed to {Value}", value);
            }
        }
    }

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

    [SuppressMessage("Microsoft.Performance", "CA1822:Mark members as static", Justification = "Instance method on ViewModel; Android-only body references instance container properties.")]
    public void StopPermissionCheckTasks()
    {
#if ANDROID
        AlarmSettingsContainerViewModel?.StopPermissionCheckTaskIfRunning();
        NumberOfTrackContainerViewModel?.StopPermissionCheckTaskIfRunning();
#endif
    }

    private void NotifySchedulePropertiesChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(DaysOfWeek));
            OnPropertyChanged(nameof(Time));
            OnPropertyChanged(nameof(MusicEnabled));
        });
    }

    private void DisposeContainers()
    {
        if (BibleSelectionContainerViewModel is IDisposable bibleDisposable)
        {
            bibleDisposable.Dispose();
        }
        BibleSelectionContainerViewModel = null;

        if (MusicSelectionContainerViewModel is IDisposable musicDisposable)
        {
            musicDisposable.Dispose();
        }
        MusicSelectionContainerViewModel = null;

        if (NumberOfTrackContainerViewModel is IDisposable tracksDisposable)
        {
            tracksDisposable.Dispose();
        }
        NumberOfTrackContainerViewModel = null;

        if (ScheduleDetailsContainerViewModel is IDisposable detailsDisposable)
        {
            detailsDisposable.Dispose();
        }
        ScheduleDetailsContainerViewModel = null;

        if (AlarmSettingsContainerViewModel is IDisposable alarmSettingsDisposable)
        {
            alarmSettingsDisposable.Dispose();
        }
        AlarmSettingsContainerViewModel = null;
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        overlayTimeoutController.Dispose();
        DisposeContainers();
        overlayManager.Dispose();
    }
}
