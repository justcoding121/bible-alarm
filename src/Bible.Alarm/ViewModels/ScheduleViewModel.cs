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
    private readonly IScheduleInitializationService scheduleInitializationService;
    private readonly IScheduleCommandService scheduleCommandService;
    private readonly IScheduleMediaCacheService scheduleMediaCacheService;
    private readonly IScheduleContainerService scheduleContainerService;
    private readonly ScheduleStateChangeHandler scheduleStateChangeHandler;

    // Helper classes
    private readonly ScheduleStateManager stateManager;
    private readonly ScheduleCommandExecutor commandExecutor;
    private readonly SchedulePropertyManager propertyManager;
    private readonly ScheduleContainerManager containerManager;
    private readonly ScheduleOverlayManager overlayManager;


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
        commandExecutor = new ScheduleCommandExecutor(scheduleCommandService, scheduleMediaCacheService, state, playbackState, dispatcher, mapper, logger);
        propertyManager = new SchedulePropertyManager(state, logger);
        containerManager = new ScheduleContainerManager(scheduleContainerService, serviceProvider);
        overlayManager = new ScheduleOverlayManager(dispatcher);

        // Initialize state handling and commands
        stateManager.InitializeStateHandling(state, () => propertyManager.IsBusy = true, () => overlayManager.ShowSchedulePageOverlay());
        commandExecutor.InitializeCommands(out var cancelCmd, out var saveCmd, out var deleteCmd);
        CancelCommand = cancelCmd;
        SaveCommand = saveCmd;
        DeleteCommand = deleteCmd;

        SetupSafetyFallback();

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
            propertyManager.ChaptersSelectionContainerViewModel = chapters;
            propertyManager.ScheduleDetailsContainerViewModel = details;
        });
    }

    private void InitializeStateHandling()
    {
        state.StateChanged += OnStateChanged;
        IsBusy = true;
        propertyManager.SetIsSchedulePageOverlayVisible(true);
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });

        var currentState = state.Value;
        if (currentState.CurrentSchedule != null)
        {
            MainThread.BeginInvokeOnMainThread(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
        }
        else
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                if (state.Value.CurrentSchedule != null && !stateManager.ModelInitialized)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
                }
            });
        }
    }

    private void SetupSafetyFallback()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            if (propertyManager.IsBusy && !stateManager.ModelInitialized)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    propertyManager.IsBusy = false;
                    overlayManager.HideHomePageOverlay();
                    overlayManager.HideSchedulePageOverlay();
                });
            }
        });
    }



    private void OnStateChanged(object? sender, EventArgs e)
    {
        stateManager.HandleStateChanged(
            state,
            (visible) => propertyManager.IsSchedulePageOverlayVisible = visible,
            () => propertyManager.NotifySchedulePropertiesChanged(),
            OnCurrentScheduleChanged);
    }


    private void OnCurrentScheduleChanged(object? sender, EventArgs e)
    {
        stateManager.HandleCurrentScheduleChanged(state);
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

    private bool musicUpdated;

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

    private bool bibleReadingUpdated;

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


    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;

        // Hide overlay when ViewModel is disposed
        overlayManager.Dispose();
    }
}

