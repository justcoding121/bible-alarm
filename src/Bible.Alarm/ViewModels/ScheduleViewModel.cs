#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
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

    private readonly IToastService popUpService;
    private readonly IMediaCacheSetupService mediaCacheSetupService;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IBibleTranslationService bibleTranslationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly IMediaService mediaService;
    private readonly IMapper mapper;

    private int lastScheduleId = -1;
    private bool modelInitialized;
    private bool isInitializingNewSchedule;
    private bool isSaving;
    private bool isScrolledToBottom;

    public BibleSelectionContainerViewModel? BibleSelectionContainerViewModel { get; set; }
    public MusicSelectionContainerViewModel? MusicSelectionContainerViewModel { get; set; }
    public ChaptersSelectionContainerViewModel? ChaptersSelectionContainerViewModel { get; set; }
    public ScheduleDetailsContainerViewModel? ScheduleDetailsContainerViewModel { get; set; }

    private readonly IPlaybackService playbackService;
    private readonly INotificationService notificationService;

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
        IMapper mapper)
    {
        var constructorStartTime = DateTime.UtcNow;
        logger.Information("[PERF] ScheduleViewModel: Constructor started at {StartTime}", constructorStartTime);

        // Initialize readonly fields
        this.logger = logger;
        this.popUpService = popUpService;
        this.playbackService = playbackService;
        this.notificationService = notificationService;
        this.bibleTranslationService = bibleTranslationService;
        this.melodyMusicService = melodyMusicService;
        this.mediaService = mediaService;
        this.mapper = mapper;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.mediaCacheSetupService = mediaCacheSetupService;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;

        InitializeContainerViewModels();
        InitializeStateHandling();
        InitializeCommands();
        SetupSafetyFallback();

        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: Constructor completed in {ElapsedMs}ms", constructorElapsed);
    }

    private void InitializeContainerViewModels()
    {
        BibleSelectionContainerViewModel = serviceProvider.GetRequiredService<BibleSelectionContainerViewModel>();
        MusicSelectionContainerViewModel = serviceProvider.GetRequiredService<MusicSelectionContainerViewModel>();
        ChaptersSelectionContainerViewModel = serviceProvider.GetRequiredService<ChaptersSelectionContainerViewModel>();
        ScheduleDetailsContainerViewModel = serviceProvider.GetRequiredService<ScheduleDetailsContainerViewModel>();
    }

    private void InitializeStateHandling()
    {
        state.StateChanged += OnStateChanged;
        IsBusy = true;

        var checkStateStartTime = DateTime.UtcNow;
        var currentState = state.Value;
        var checkStateElapsed = (DateTime.UtcNow - checkStateStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: State check took {ElapsedMs}ms, CurrentSchedule={HasSchedule}",
            checkStateElapsed, currentState.CurrentSchedule != null);

        if (currentState.CurrentSchedule != null)
        {
            logger.Information("[PERF] ScheduleViewModel: Schedule already in state, triggering OnCurrentScheduleChanged");
            MainThread.BeginInvokeOnMainThread(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
        }
        else
        {
            logger.Information("[PERF] ScheduleViewModel: Schedule not in state, setting up delayed check");
            SetupDelayedStateCheck();
        }
    }

    private void SetupDelayedStateCheck()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            var delayedState = state.Value;
            if (delayedState.CurrentSchedule != null && !modelInitialized)
            {
                logger.Information("[PERF] ScheduleViewModel: Schedule found after delay, triggering OnCurrentScheduleChanged");
                await MainThread.InvokeOnMainThreadAsync(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
            }
        });
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
                });
            }
        });
    }

    private void InitializeCommands()
    {
        CancelCommand = new AsyncRelayCommand(navigationService.NavigateToHomeAsync);
        SaveCommand = new AsyncRelayCommand(ExecuteSaveCommand);
        DeleteCommand = new AsyncRelayCommand(ExecuteDeleteCommand);
    }

    private async Task ExecuteSaveCommand()
    {
        logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            IsNewSchedule, scheduleId, Name);

        isSaving = true;

        try
        {
            await ShowSaveOverlay();
            await ValidateNotificationPermissions();
            await StopPlaybackIfNeeded();
            var saved = await SaveAsync();
            await HandleSaveResult(saved);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task ShowSaveOverlay()
    {
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
            await Task.Delay(50);
        });
    }

    private async Task ValidateNotificationPermissions()
    {
        if (IsEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.WinUI) &&
            !await notificationService.CanScheduleAsync())
        {
            IsEnabled = false;
        }
    }

    private async Task StopPlaybackIfNeeded()
    {
        if (!IsNewSchedule &&
            playbackState.Value.IsPreparingOrPlaying &&
            scheduleId == playbackState.Value.CurrentScheduleId)
        {
            await playbackService.StopAsync();
        }
    }

    private async Task HandleSaveResult(bool saved)
    {
        if (saved)
        {
            logger.Information("SaveCommand: Save successful, navigating to home. ScheduleId={ScheduleId}", scheduleId);
            await Task.Delay(100);
            await navigationService.NavigateToHomeAsync();
        }
        else
        {
            logger.Error("SaveCommand: Save failed, hiding overlay. ScheduleId={ScheduleId}", scheduleId);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        }

        if (saved && IsEnabled)
        {
            await popUpService.ShowScheduledNotification(Model);
        }
    }

    private async Task ExecuteDeleteCommand()
    {
        await ShowDeleteOverlay();

        if (IsNewSchedule)
        {
            await navigationService.NavigateToHomeAsync();
            return;
        }

        await StopPlaybackIfNeeded();
        await DeleteAsync();
        await navigationService.NavigateToHomeAsync();
    }

    private async Task ShowDeleteOverlay()
    {
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
            await Task.Delay(50);
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
        logger.Information("[PERF] OnCurrentScheduleChanged: Processing schedule Id={ScheduleId}, LastScheduleId={LastScheduleId}, ModelInitialized={ModelInitialized}",
            currentScheduleId, lastScheduleId, modelInitialized);

        if (currentScheduleId == lastScheduleId && modelInitialized)
        {
            if (isSaving)
            {
                logger.Debug("OnCurrentScheduleChanged: Skipping model update during save operation. ScheduleId={ScheduleId}", currentScheduleId);
                return;
            }

            HandleScheduleUpdateFromState(stateValue, currentScheduleId);
            return;
        }

        LoadScheduleFromState(stateValue, currentScheduleId);
    }

    private void HandleScheduleUpdateFromState(ApplicationState stateValue, int currentScheduleId)
    {
        var updatedScheduleItem = stateValue.Schedules?.FirstOrDefault(s => s.Id == currentScheduleId);
        if (updatedScheduleItem != null)
        {
            var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
            if (updatedSchedule.Id != Model.Id)
            {
                MainThread.BeginInvokeOnMainThread(() => SetModel(updatedSchedule));
            }
        }
    }

    private void LoadScheduleFromState(ApplicationState stateValue, int currentScheduleId)
    {
        isInitializingNewSchedule = false;
        var currentScheduleItem = stateValue.CurrentSchedule!;
        lastScheduleId = currentScheduleId;

        var isNew = currentScheduleItem.Id <= 0;
        logger.Information("[PERF] OnCurrentScheduleChanged: IsNew={IsNew}, invoking on main thread", isNew);

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var mainThreadStartTime = DateTime.UtcNow;
                logger.Information("[PERF] OnCurrentScheduleChanged: Main thread handler started at {StartTime}", mainThreadStartTime);

                IsNewSchedule = isNew;
                var currentSchedule = mapper.Map<AlarmSchedule>(currentScheduleItem);

                var setModelStartTime = DateTime.UtcNow;
                SetModel(currentSchedule);
                var setModelElapsed = (DateTime.UtcNow - setModelStartTime).TotalMilliseconds;
                logger.Information("[PERF] OnCurrentScheduleChanged: SetModel took {ElapsedMs}ms", setModelElapsed);

                await CompleteScheduleLoad();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in OnCurrentScheduleChanged handler");
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
            }
        });
    }

    private async Task CompleteScheduleLoad()
    {
        modelInitialized = true;
        await Task.Delay(100);
        IsBusy = false;
        OnPropertyChanged(nameof(IsBusy));
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
        logger.Debug("OnCurrentScheduleChanged: Resetting ViewModel for new schedule. Previous ScheduleId={PreviousScheduleId}", scheduleId);
        modelInitialized = false;
        lastScheduleId = -1;
        scheduleId = 0;
        IsNewSchedule = false;
    }

    private void InitializeNewSchedule()
    {
        isInitializingNewSchedule = true;
        logger.Information("[PERF] OnCurrentScheduleChanged: Starting new schedule initialization");
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
                    logger.Debug("OnCurrentScheduleChanged: Initializing new schedule. SampleSchedule.Id={SampleScheduleId}", sampleSchedule.Id);

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
                IsBusy = false;
            });
        });
    }

    public ICommand CancelCommand { get; set; }

    public ICommand SaveCommand { get; set; }
    public ICommand DeleteCommand { get; set; }

    public AlarmSchedule Model { get; private set; }

    private AlarmSchedule GetModel()
    {
        logger.Debug("GetModel: Reading ViewModel properties. ViewModel.Name={ViewModelName}, ViewModel.MusicEnabled={ViewModelMusicEnabled}, ViewModel.IsEnabled={ViewModelIsEnabled}",
            Name, MusicEnabled, IsEnabled);

        Model.Id = scheduleId;

        // Container ViewModels update Model directly, so we just need to ensure MusicEnabled is set
        Model.MusicEnabled = MusicEnabled;

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

        InitializeContainerViewModels(model);
    }

    private void InitializeContainerViewModels(AlarmSchedule model)
    {
        if (ScheduleDetailsContainerViewModel != null)
        {
            ScheduleDetailsContainerViewModel.Initialize(Model, Time, DaysOfWeek, Name, IsEnabled);
        }

        if (BibleSelectionContainerViewModel != null)
        {
            BibleSelectionContainerViewModel.Initialize(scheduleId, IsNewSchedule, Model, Model.BibleReadingSchedule, bibleReadingUpdated);
        }

        if (ChaptersSelectionContainerViewModel != null)
        {
            ChaptersSelectionContainerViewModel.Initialize(scheduleId, Model, model.NotificationEnabled, model.AlwaysPlayFromStart);
        }

        if (MusicSelectionContainerViewModel != null)
        {
            MusicSelectionContainerViewModel.Initialize(scheduleId, IsNewSchedule, Model, Model.Music, musicUpdated);
        }
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

        if (!await ValidateSavePreconditions())
        {
            return false;
        }

        if (IsNewSchedule)
        {
            IsEnabled = true;
        }

        var model = PrepareModelForSave();
        DispatchSaveAction(model);
        SetupMediaCache(model.Id, isUpdate: !IsNewSchedule);

        return true;
    }

    private async Task<bool> ValidateSavePreconditions()
    {
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

        return true;
    }

    private AlarmSchedule PrepareModelForSave()
    {
        var model = GetModel();

        EnsureDefaultPublicationCode(model);
        ClearUnchangedMusic(model);

        logger.Debug("SaveAsync: Model retrieved. Model.Id={ModelId}, Model.Name={ModelName}, HasMusic={HasMusic}, HasBibleReading={HasBibleReading}, MusicEnabled={MusicEnabled}, PublicationCode={PublicationCode}",
            model.Id, model.Name, model.Music != null, model.BibleReadingSchedule != null, model.MusicEnabled,
            model.BibleReadingSchedule?.PublicationCode ?? "null");

        return model;
    }

    private void EnsureDefaultPublicationCode(AlarmSchedule model)
    {
        if (model.BibleReadingSchedule != null && string.IsNullOrWhiteSpace(model.BibleReadingSchedule.PublicationCode))
        {
            logger.Warning("SaveAsync: BibleReadingSchedule has empty PublicationCode, defaulting to 'nwt' (2013)");
            model.BibleReadingSchedule.PublicationCode = "nwt";
        }
    }

    private void ClearUnchangedMusic(AlarmSchedule model)
    {
        if (!IsNewSchedule && !musicUpdated)
        {
            model.Music = null;
            logger.Debug("SaveAsync: Music set to null for existing schedule (not updated)");
        }
    }

    private void DispatchSaveAction(AlarmSchedule model)
    {
        logger.Information("SaveAsync: Mapping AlarmSchedule to ScheduleStateItem and dispatching action. IsNewSchedule={IsNewSchedule}, MusicUpdated={MusicUpdated}, BibleReadingUpdated={BibleReadingUpdated}",
            IsNewSchedule, musicUpdated, bibleReadingUpdated);

        var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);

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
    }
}
