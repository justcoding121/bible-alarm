#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
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
    
    // Track if content has been loaded
    private bool isContentLoaded;


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
        IBibleTranslationService bibleTranslationService,
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
        // Initialize readonly fields
        this.logger = logger;
        this.mapper = mapper;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.serviceProvider = serviceProvider;

        // Initialize helper classes
        stateManager = new ScheduleStateManager(scheduleInitializationService, scheduleStateChangeHandler, dispatcher, logger);
        commandExecutor = new ScheduleCommandExecutor(scheduleCommandService, scheduleMediaCacheService, state, playbackState, dispatcher, mapper, logger, () => propertyManager?.MusicSelectionContainerViewModel);
        propertyManager = new SchedulePropertyManager(state, logger);
        containerManager = new ScheduleContainerManager(scheduleContainerService, serviceProvider);
        overlayManager = new ScheduleOverlayManager(dispatcher);

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
            // Forward container ViewModel property changes
            else if (e.PropertyName == nameof(SchedulePropertyManager.BibleSelectionContainerViewModel))
            {
                OnPropertyChanged(nameof(BibleSelectionContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.MusicSelectionContainerViewModel))
            {
                OnPropertyChanged(nameof(MusicSelectionContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.NumberOfChapterContainerViewModel))
            {
                OnPropertyChanged(nameof(NumberOfChapterContainerViewModel));
            }
            else if (e.PropertyName == nameof(SchedulePropertyManager.ScheduleDetailsContainerViewModel))
            {
                OnPropertyChanged(nameof(ScheduleDetailsContainerViewModel));
            }
        };

        // Subscribe to state changes
        state.StateChanged += OnStateChanged;

        // Initialize IsNewSchedule immediately from current state
        // This ensures the Delete button visibility is correct from the start
        var initialSchedule = state.Value.CurrentSchedule;
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
    }

    /// <summary>
    /// Initializes container view models asynchronously off the UI thread.
    /// This prevents blocking the UI thread during page load.
    /// Containers are created in Task.Run, then assigned on the UI thread.
    /// </summary>
    private async Task InitializeContainerViewModelsAsync()
    {
        await containerManager.InitializeContainerViewModelsAsync((bible, music, chapters, details) =>
        {
            propertyManager.BibleSelectionContainerViewModel = bible;
            propertyManager.MusicSelectionContainerViewModel = music;
            propertyManager.NumberOfChapterContainerViewModel = chapters;
            propertyManager.ScheduleDetailsContainerViewModel = details;
        });
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
        });

        // Check if we should hide overlay: both containers ready AND content loaded
        // Also call OnContentLoaded when containers become ready (handles case where containers become ready after content loads)
        if (stateValue.ContainerReadiness.AllReady && isContentLoaded)
        {
            if (stateValue.IsSchedulePageOverlayVisible)
            {
                logger.Debug("ScheduleViewModel: All containers ready and content loaded, hiding overlay");
                dispatcher.Dispatch(new global::Bible.Alarm.Stores.Actions.SetSchedulePageOverlayAction { IsVisible = false });
            }
            else
            {
                // Containers became ready after overlay was already hidden - this is fine
                logger.Debug("ScheduleViewModel: All containers ready, but overlay already hidden");
            }
        }
        else if (stateValue.ContainerReadiness.AllReady && !isContentLoaded)
        {
            logger.Debug("ScheduleViewModel: All containers ready, waiting for content to load before hiding overlay");
        }
        else if (!stateValue.ContainerReadiness.AllReady && isContentLoaded)
        {
            // Content is loaded but containers aren't ready yet - log for debugging
            logger.Debug("ScheduleViewModel: Content loaded but containers not ready yet. BibleSelection={Bible}, MusicSelection={Music}, NumberOfChapter={Number}, ScheduleDetails={Schedule}",
                stateValue.ContainerReadiness.BibleSelection,
                stateValue.ContainerReadiness.MusicSelection,
                stateValue.ContainerReadiness.NumberOfChapter,
                stateValue.ContainerReadiness.ScheduleDetails);
        }
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

    public BibleReadingSchedule? BibleReadingSchedule
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return null;
            }
            return mapper.Map<AlarmSchedule>(currentSchedule).BibleReadingSchedule;
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

    // Container ViewModels - exposed for XAML binding
    public BibleSelectionContainerViewModel? BibleSelectionContainerViewModel => propertyManager.BibleSelectionContainerViewModel;
    public MusicSelectionContainerViewModel? MusicSelectionContainerViewModel => propertyManager.MusicSelectionContainerViewModel;
    public NumberOfChapterContainerViewModel? NumberOfChapterContainerViewModel => propertyManager.NumberOfChapterContainerViewModel;
    public ScheduleDetailsContainerViewModel? ScheduleDetailsContainerViewModel => propertyManager.ScheduleDetailsContainerViewModel;

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
        logger.Debug("ScheduleViewModel: Resetting isContentLoaded flag");
        isContentLoaded = false;
    }

    /// <summary>
    /// Called by page code-behind after content is loaded.
    /// Hides overlay if all containers are ready.
    /// </summary>
    public void OnContentLoaded()
    {
        isContentLoaded = true;
        
        var stateValue = state.Value;
        var allReady = stateValue.ContainerReadiness.AllReady;
        var overlayVisible = stateValue.IsSchedulePageOverlayVisible;
        
        logger.Debug("ScheduleViewModel: OnContentLoaded called - AllReady={AllReady}, OverlayVisible={OverlayVisible}, isContentLoaded={IsContentLoaded}", 
            allReady, overlayVisible, isContentLoaded);
        
        // Check if we should hide overlay: both containers ready AND content loaded
        if (allReady && overlayVisible)
        {
            logger.Debug("ScheduleViewModel: Content loaded and all containers ready, hiding overlay");
            dispatcher.Dispatch(new global::Bible.Alarm.Stores.Actions.SetSchedulePageOverlayAction { IsVisible = false });
        }
        else
        {
            logger.Debug("ScheduleViewModel: OnContentLoaded - Condition not met. AllReady={AllReady}, OverlayVisible={OverlayVisible}", 
                allReady, overlayVisible);
        }
    }


    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;

        // Hide overlay when ViewModel is disposed
        overlayManager.Dispose();
    }
}

