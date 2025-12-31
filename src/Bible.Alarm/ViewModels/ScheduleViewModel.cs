#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Helpers;
using Bible.Alarm.Services.Schedule.Interfaces;
using static Bible.Alarm.Services.Schedule.Helpers.SchedulePropertyHelper;
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

    private int lastScheduleId = -1;
    private bool modelInitialized;
    private bool isInitializingNewSchedule;
    private bool isSaving;
    private bool isScrolledToBottom;

    // Track previous music state to detect changes
    private MusicType? lastMusicType;
    private int? lastMusicTrackNumber;
    private string? lastMusicPublicationCode;
    private string? lastMusicLanguageCode;
    private bool? lastMusicRepeat;

    // Track last processed state to prevent redundant processing
    private int lastProcessedScheduleId = -1;
    private bool lastProcessedOverlayVisible = true;

    public BibleSelectionContainerViewModel? BibleSelectionContainerViewModel { get; set; }
    public MusicSelectionContainerViewModel? MusicSelectionContainerViewModel { get; set; }
    public ChaptersSelectionContainerViewModel? ChaptersSelectionContainerViewModel { get; set; }
    public ScheduleDetailsContainerViewModel? ScheduleDetailsContainerViewModel { get; set; }


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
        this.scheduleInitializationService = scheduleInitializationService;
        this.scheduleCommandService = scheduleCommandService;
        this.scheduleMediaCacheService = scheduleMediaCacheService;
        this.scheduleContainerService = scheduleContainerService;
        this.scheduleStateChangeHandler = scheduleStateChangeHandler;

        // Defer container initialization - will be created after page is visible
        // This prevents blocking the UI thread during page load
        InitializeStateHandling();
        InitializeCommands();
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
        await scheduleContainerService.InitializeContainersAsync(serviceProvider, (bible, music, chapters, details) =>
        {
            BibleSelectionContainerViewModel = bible;
            MusicSelectionContainerViewModel = music;
            ChaptersSelectionContainerViewModel = chapters;
            ScheduleDetailsContainerViewModel = details;
        });
    }

    private void InitializeStateHandling()
    {
        state.StateChanged += OnStateChanged;
        IsBusy = true;
        modelInitialized = false;
        IsSchedulePageOverlayVisible = true;
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
                if (state.Value.CurrentSchedule != null && !modelInitialized)
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
            if (IsBusy && !modelInitialized)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
                    dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                });
            }
        });
    }

    private void InitializeCommands()
    {
        CancelCommand = new AsyncRelayCommand(ExecuteCancelCommand);
        SaveCommand = new AsyncRelayCommand(ExecuteSaveCommand);
        DeleteCommand = new AsyncRelayCommand(ExecuteDeleteCommand);
    }

    private async Task ExecuteCancelCommand()
    {
        await scheduleCommandService.ExecuteCancelAsync(IsNewSchedule, ScheduleId, state.Value.CurrentSchedule);
    }


    private async Task ExecuteSaveCommand()
    {
        logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            IsNewSchedule, ScheduleId, Name);

        isSaving = true;

        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            await scheduleCommandService.ValidateNotificationPermissionsAsync(currentSchedule);

            await scheduleCommandService.StopPlaybackIfNeededAsync(
                IsNewSchedule,
                playbackState.Value.IsPreparingOrPlaying,
                ScheduleId,
                playbackState.Value.CurrentScheduleId ?? -1);

            if (currentSchedule != null)
            {
                var saved = await scheduleCommandService.ExecuteSaveAsync(
                    IsNewSchedule,
                    ScheduleId,
                    currentSchedule,
                    musicUpdated,
                    bibleReadingUpdated,
                    modelInitialized);

                if (saved)
                {
                    scheduleMediaCacheService.SetupMediaCache(ScheduleId, isUpdate: !IsNewSchedule);
                }

                var model = GetModel();
                await scheduleCommandService.HandleSaveResultAsync(saved, ScheduleId, IsEnabled, model);
            }
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task ExecuteDeleteCommand()
    {
        await scheduleCommandService.StopPlaybackIfNeededAsync(
            IsNewSchedule,
            playbackState.Value.IsPreparingOrPlaying,
            ScheduleId,
            playbackState.Value.CurrentScheduleId ?? -1);

        var scheduleCount = state.Value.Schedules?.Count ?? 0;
        await scheduleCommandService.ExecuteDeleteAsync(IsNewSchedule, ScheduleId, scheduleCount);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentScheduleId = stateValue.CurrentSchedule?.Id ?? -1;
        var newOverlayVisible = stateValue.IsSchedulePageOverlayVisible;
        var isScheduleUpdate = currentScheduleId == lastScheduleId && modelInitialized;

        // Early exit if we've already processed this exact state (only for schedule updates, not initial loads)
        // Also check if the property value itself hasn't changed to prevent unnecessary updates
        if (isScheduleUpdate && 
            currentScheduleId == lastProcessedScheduleId && 
            newOverlayVisible == lastProcessedOverlayVisible &&
            newOverlayVisible == isSchedulePageOverlayVisible)
        {
            logger.Debug("ScheduleViewModel: OnStateChanged - Skipping processing as no relevant changes detected. ScheduleId: {ScheduleId}, OverlayVisible: {OverlayVisible}", 
                currentScheduleId, newOverlayVisible);
            return;
        }

        // Early exit if property value already matches state value (prevents unnecessary PropertyChanged events)
        // This is especially important when visiting the same schedule multiple times
        // Check this BEFORE checking isScheduleUpdate to catch all cases where property already matches
        if (newOverlayVisible == isSchedulePageOverlayVisible)
        {
            // Still update tracking fields to prevent future unnecessary processing
            // Only update if this is a schedule update (not initial load) to avoid interfering with initialization
            if (isScheduleUpdate)
            {
                lastProcessedScheduleId = currentScheduleId;
                lastProcessedOverlayVisible = newOverlayVisible;
            }
            // Always return early if property already matches - no need to process further
            return;
        }

        // Only update property if value actually changed to prevent unnecessary PropertyChanged events
        if (newOverlayVisible != isSchedulePageOverlayVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Double-check the value hasn't changed since we checked (race condition protection)
                if (newOverlayVisible != isSchedulePageOverlayVisible)
                {
                    if (!newOverlayVisible || !isScheduleUpdate)
                    {
                        IsSchedulePageOverlayVisible = newOverlayVisible;
                    }
                }
            });
        }

        NotifySchedulePropertiesChanged();

        if (currentScheduleId != lastScheduleId || !modelInitialized)
        {
            OnCurrentScheduleChanged(sender, e);
        }

        // Update last processed state after handling changes
        lastProcessedScheduleId = currentScheduleId;
        lastProcessedOverlayVisible = newOverlayVisible;
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

    private void OnCurrentScheduleChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule != null)
        {
            HandleExistingScheduleUpdate(stateValue);
        }
        else
        {
            HandleNewScheduleInitialization(stateValue);
        }
    }

    private void HandleExistingScheduleUpdate(ApplicationState stateValue)
    {
        var currentScheduleId = stateValue.CurrentSchedule!.Id;

        if (currentScheduleId == lastScheduleId && modelInitialized)
        {
            if (isSaving) return;
            HandleScheduleUpdateFromState(stateValue, currentScheduleId);
            // Only dispatch if overlay is currently visible to avoid infinite loops
            if (stateValue.IsSchedulePageOverlayVisible)
            {
                dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            }
            return;
        }

        // Reset tracking fields when schedule ID changes
        lastProcessedScheduleId = -1;
        lastProcessedOverlayVisible = true;
        
        LoadScheduleFromState(stateValue, currentScheduleId);
    }

    private bool HandleScheduleUpdateFromState(ApplicationState stateValue, int currentScheduleId)
    {
        var currentSchedule = stateValue.CurrentSchedule;
        var hasChanges = scheduleStateChangeHandler.HandleScheduleUpdateFromState(
            currentSchedule,
            ref lastMusicType,
            ref lastMusicTrackNumber,
            ref lastMusicPublicationCode,
            ref lastMusicLanguageCode,
            ref lastMusicRepeat,
            out var musicChanged);

        if (musicChanged)
        {
            musicUpdated = true;
        }

        if (hasChanges)
        {
            NotifySchedulePropertiesChanged();
        }

        return hasChanges;
    }

    private void LoadScheduleFromState(ApplicationState stateValue, int currentScheduleId)
    {
        isInitializingNewSchedule = false;
        var currentScheduleItem = stateValue.CurrentSchedule!;

        // Reset tracking fields when loading a new schedule
        lastProcessedScheduleId = -1;
        lastProcessedOverlayVisible = stateValue.IsSchedulePageOverlayVisible;

        _ = Task.Run(async () =>
        {
            try
            {
                var scheduleStateItemSnapshot = currentScheduleItem.DeepClone();
                scheduleInitializationService.InitializeTrackingFields(
                    scheduleStateItemSnapshot,
                    ref lastScheduleId,
                    ref lastMusicType,
                    ref lastMusicTrackNumber,
                    ref lastMusicPublicationCode,
                    ref lastMusicLanguageCode,
                    ref lastMusicRepeat);

                musicUpdated = false;
                bibleReadingUpdated = false;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        IsNewSchedule = scheduleStateItemSnapshot.Id <= 0;
                        NotifySchedulePropertiesChanged();
                        await CompleteScheduleLoadAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in LoadScheduleFromState main thread handler");
                        HandleLoadError();
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in LoadScheduleFromState data preparation");
                await MainThread.InvokeOnMainThreadAsync(HandleLoadError);
            }
        });
    }

    private async Task CompleteScheduleLoadAsync()
    {
        await scheduleInitializationService.CompleteScheduleLoadAsync();
        IsBusy = false;
        OnPropertyChanged(nameof(IsBusy));

        var maxWaitTime = TimeSpan.FromMilliseconds(500);
        var startTime = DateTime.UtcNow;
        while ((BibleSelectionContainerViewModel == null ||
                MusicSelectionContainerViewModel == null ||
                ChaptersSelectionContainerViewModel == null ||
                ScheduleDetailsContainerViewModel == null) &&
               (DateTime.UtcNow - startTime) < maxWaitTime)
        {
            await Task.Delay(50);
        }

        await Task.Delay(50);
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        await Task.Delay(50);
        OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
    }

    private void HandleLoadError()
    {
        IsBusy = false;
        OnPropertyChanged(nameof(IsBusy));
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
    }


    private void HandleNewScheduleInitialization(ApplicationState stateValue)
    {
        if (modelInitialized && !isSaving)
        {
            ResetViewModelForNewSchedule();
        }
        if (!modelInitialized && !isInitializingNewSchedule)
        {
            InitializeNewSchedule();
        }
    }

    private void ResetViewModelForNewSchedule()
    {
        modelInitialized = false;
        lastScheduleId = -1;
        IsNewSchedule = false;
        scheduleStateChangeHandler.ResetMusicTrackingFields(
            ref lastMusicType,
            ref lastMusicTrackNumber,
            ref lastMusicPublicationCode,
            ref lastMusicLanguageCode,
            ref lastMusicRepeat);
        musicUpdated = false;
        bibleReadingUpdated = false;
    }

    private void InitializeNewSchedule()
    {
        isInitializingNewSchedule = true;
        MainThread.BeginInvokeOnMainThread(() => IsBusy = true);

        Task.Run(async () =>
        {
            var scheduleStateItem = await scheduleInitializationService.InitializeNewScheduleAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var currentState = state.Value;
                if (isInitializingNewSchedule && !modelInitialized && currentState.CurrentSchedule == null)
                {
                    modelInitialized = true;
                    scheduleInitializationService.InitializeTrackingFields(
                        scheduleStateItem,
                        ref lastScheduleId,
                        ref lastMusicType,
                        ref lastMusicTrackNumber,
                        ref lastMusicPublicationCode,
                        ref lastMusicLanguageCode,
                        ref lastMusicRepeat);

                    dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
                    IsNewSchedule = true;
                    musicUpdated = false;
                    bibleReadingUpdated = false;
                    NotifySchedulePropertiesChanged();
                    await FinalizeNewScheduleInitialization();
                }
                else
                {
                    await FinalizeNewScheduleInitialization();
                }
            });
        });
    }

    private async Task FinalizeNewScheduleInitialization()
    {
        isInitializingNewSchedule = false;
        IsBusy = false;
        OnPropertyChanged(nameof(IsBusy));
        await Task.Delay(400);
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        await Task.Delay(100);
        OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
    }

    public ICommand CancelCommand { get; set; } = null!;

    public ICommand SaveCommand { get; set; } = null!;
    public ICommand DeleteCommand { get; set; } = null!;

    private AlarmSchedule GetModel()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return new AlarmSchedule { Id = 0 };
        }
        return mapper.Map<AlarmSchedule>(currentSchedule);
    }

    private int ScheduleId => SchedulePropertyHelper.GetScheduleId(state.Value.CurrentSchedule);

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }


    public string Name => SchedulePropertyHelper.GetName(state.Value.CurrentSchedule);

    public bool IsEnabled => SchedulePropertyHelper.GetIsEnabled(state.Value.CurrentSchedule);

    public DaysOfWeek DaysOfWeek => SchedulePropertyHelper.GetDaysOfWeek(state.Value.CurrentSchedule);

    public TimeSpan Time => SchedulePropertyHelper.GetTime(state.Value.CurrentSchedule);

    public bool MusicEnabled => SchedulePropertyHelper.GetMusicEnabled(state.Value.CurrentSchedule);

    private bool musicUpdated;

    public AlarmMusic? Music
    {
        get => GetModel().Music;
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
        get => GetModel().BibleReadingSchedule;
        set
        {
            // Bible reading updates are handled by BibleSelectionContainerViewModel via actions
            // This setter is kept for backward compatibility but doesn't need to do anything
            // as the container view model dispatches actions directly
        }
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




    /// <summary>
    /// Hides the Home page overlay. Called when the Schedule page is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay() => dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });

    // Start as true to show busy indicator overlay immediately
    private bool isSchedulePageOverlayVisible = true;

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
            if (SetProperty(ref isSchedulePageOverlayVisible, value))
            {
                logger.Debug("IsSchedulePageOverlayVisible: Property changed to {Value}", value);
            }
        }
    }

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });


    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;

        // Hide overlay when ViewModel is disposed
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
    }
}

