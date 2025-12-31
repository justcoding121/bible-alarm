#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
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
    private readonly IAlarmScheduleService alarmScheduleService;

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
        IMapper mapper,
        IAlarmScheduleService alarmScheduleService)
    {
#if DEBUG
        var constructorStartTime = DateTime.UtcNow;
        logger.Information("[PERF] ScheduleViewModel: Constructor started at {StartTime}", constructorStartTime);
#endif

        // Initialize readonly fields
        this.logger = logger;
        this.popUpService = popUpService;
        this.playbackService = playbackService;
        this.notificationService = notificationService;
        this.bibleTranslationService = bibleTranslationService;
        this.melodyMusicService = melodyMusicService;
        this.mediaService = mediaService;
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.mediaCacheSetupService = mediaCacheSetupService;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;

        // Defer container initialization - will be created after page is visible
        // This prevents blocking the UI thread during page load
        InitializeStateHandling();
        InitializeCommands();
        SetupSafetyFallback();
        
        // Initialize containers asynchronously after page is visible
        _ = InitializeContainerViewModelsAsync();

#if DEBUG
        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: Constructor completed in {ElapsedMs}ms", constructorElapsed);
#endif
    }

    /// <summary>
    /// Initializes container view models asynchronously off the UI thread.
    /// This prevents blocking the UI thread during page load.
    /// Containers are created in Task.Run, then assigned on the UI thread.
    /// </summary>
    private async Task InitializeContainerViewModelsAsync()
    {
#if DEBUG
        var containerInitStartTime = DateTime.UtcNow;
        logger.Information("[PERF] ScheduleViewModel: Starting async container initialization at {StartTime}", containerInitStartTime);
#endif

        try
        {
            // Create container view models off UI thread (DI resolution can be CPU-intensive)
            var containers = await Task.Run(() =>
            {
                return new
                {
                    BibleSelection = serviceProvider.GetRequiredService<BibleSelectionContainerViewModel>(),
                    MusicSelection = serviceProvider.GetRequiredService<MusicSelectionContainerViewModel>(),
                    ChaptersSelection = serviceProvider.GetRequiredService<ChaptersSelectionContainerViewModel>(),
                    ScheduleDetails = serviceProvider.GetRequiredService<ScheduleDetailsContainerViewModel>()
                };
            });

#if DEBUG
            var containerInitElapsed = (DateTime.UtcNow - containerInitStartTime).TotalMilliseconds;
            logger.Information("[PERF] ScheduleViewModel: Container creation completed in {ElapsedMs}ms", containerInitElapsed);
#endif

            // Assign containers on UI thread (property assignments need UI thread for bindings)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BibleSelectionContainerViewModel = containers.BibleSelection;
                MusicSelectionContainerViewModel = containers.MusicSelection;
                ChaptersSelectionContainerViewModel = containers.ChaptersSelection;
                ScheduleDetailsContainerViewModel = containers.ScheduleDetails;

#if DEBUG
                logger.Information("[PERF] ScheduleViewModel: Containers assigned on UI thread");
#endif
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing container view models");
            // Continue even if container initialization fails - page can still function
        }
    }

    private void InitializeStateHandling()
    {
        state.StateChanged += OnStateChanged;
        IsBusy = true;
        
        // Reset modelInitialized flag when page opens to ensure proper loading flow
        modelInitialized = false;
        
        // Start with overlay visible by default (will be hidden after initialization completes)
        IsSchedulePageOverlayVisible = true;
        
        // Show overlay when page opens
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });

#if DEBUG
        var checkStateStartTime = DateTime.UtcNow;
#endif
        var currentState = state.Value;
#if DEBUG
        var checkStateElapsed = (DateTime.UtcNow - checkStateStartTime).TotalMilliseconds;
        logger.Information("[PERF] ScheduleViewModel: State check took {ElapsedMs}ms, CurrentSchedule={HasSchedule}",
            checkStateElapsed, currentState.CurrentSchedule != null);
#endif

        if (currentState.CurrentSchedule != null)
        {
#if DEBUG
            logger.Information("[PERF] ScheduleViewModel: Schedule already in state, triggering OnCurrentScheduleChanged");
#endif
            MainThread.BeginInvokeOnMainThread(() => OnCurrentScheduleChanged(this, EventArgs.Empty));
        }
        else
        {
#if DEBUG
            logger.Information("[PERF] ScheduleViewModel: Schedule not in state, setting up delayed check");
#endif
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
#if DEBUG
                logger.Information("[PERF] ScheduleViewModel: Schedule found after delay, triggering OnCurrentScheduleChanged");
#endif
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
        logger.Information("CancelCommand: Cancel button clicked. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}", ScheduleId, IsNewSchedule);

        // If it's a new schedule, remove it from state and navigate home
        if (IsNewSchedule)
        {
            logger.Debug("CancelCommand: New schedule, removing from state and navigating to home");

            // Remove any unsaved schedule (ID <= 0) from the Schedules collection
            var currentState = state.Value;
            if (currentState.Schedules != null)
            {
                var unsavedSchedules = currentState.Schedules.Where(s => s.Id <= 0).ToList();
                foreach (var unsavedSchedule in unsavedSchedules)
                {
                    logger.Debug("CancelCommand: Removing unsaved schedule with ID {ScheduleId} from state", unsavedSchedule.Id);
                    dispatcher.Dispatch(new RemoveScheduleSuccessAction(unsavedSchedule.Id));
                }
            }

            // Reset schedule state
            dispatcher.Dispatch(new ResetScheduleStateAction());
            
            // Hide overlay when navigating away
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            
            await navigationService.NavigateToHomeAsync();
            return;
        }

        // For existing schedules, reload from database to revert optimistic changes
        if (ScheduleId > 0)
        {
            logger.Debug("CancelCommand: Existing schedule, reloading from database to revert optimistic changes. ScheduleId={ScheduleId}", ScheduleId);

            try
            {
                // Reload schedule from database
                var reloadedSchedule = await Task.Run(async () =>
                    await alarmScheduleService.GetScheduleByIdAsync(ScheduleId, true, true, CancellationToken.None));

                if (reloadedSchedule != null)
                {
                    // Map to ScheduleStateItem and populate display names
                    var scheduleStateItem = mapper.Map<ScheduleStateItem>(reloadedSchedule);

                    // Populate display names (these are not stored in DB, need to be populated)
                    await PopulateDisplayNamesAsync(scheduleStateItem, reloadedSchedule);

                    // Update the schedule in the Schedules collection to revert optimistic changes
                    logger.Information("CancelCommand: Reloaded schedule from database. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
                        scheduleStateItem.BibleReadingLanguageCode ?? "null",
                        scheduleStateItem.BibleReadingPublicationCode ?? "null",
                        scheduleStateItem.BibleReadingBookNumber ?? 0,
                        scheduleStateItem.BibleReadingChapterNumber ?? 0);

                    // Dispatch action to update the schedule in Schedules collection
                    dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));
                }
                else
                {
                    logger.Warning("CancelCommand: Failed to reload schedule from database. ScheduleId={ScheduleId}", ScheduleId);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "CancelCommand: Error reloading schedule from database. ScheduleId={ScheduleId}", ScheduleId);
            }
        }

        // Clear CurrentSchedule after cancel (discard draft changes)
        dispatcher.Dispatch(new ResetScheduleStateAction());
        
        // Hide overlay when navigating away
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

        // Navigate to home
        await navigationService.NavigateToHomeAsync();
    }

    private async Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        // Populate Bible reading display names
        if (schedule.BibleReadingSchedule != null)
        {
            // Language name
            if (!string.IsNullOrWhiteSpace(schedule.BibleReadingSchedule.LanguageCode) && bibleTranslationService != null)
            {
                try
                {
                    // Run database operations off UI thread
                    var languagesDict = await Task.Run(async () =>
                        await bibleTranslationService.GetDistinctLanguagesAsync());
                    if (languagesDict.TryGetValue(schedule.BibleReadingSchedule.LanguageCode, out var language))
                    {
                        scheduleStateItem.BibleReadingLanguageName = language.Name;
                    }
                    else
                    {
                        scheduleStateItem.BibleReadingLanguageName = schedule.BibleReadingSchedule.LanguageCode;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "CancelCommand: Error populating BibleReadingLanguageName");
                    scheduleStateItem.BibleReadingLanguageName = schedule.BibleReadingSchedule.LanguageCode;
                }
            }

            // Translation name
            if (!string.IsNullOrWhiteSpace(schedule.BibleReadingSchedule.LanguageCode) &&
                !string.IsNullOrWhiteSpace(schedule.BibleReadingSchedule.PublicationCode) &&
                bibleTranslationService != null)
            {
                try
                {
                    // Run database operations off UI thread
                    var translation = await Task.Run(async () =>
                        await bibleTranslationService.GetByLanguageAndCodeWithBooksAsync(
                            schedule.BibleReadingSchedule.LanguageCode,
                            schedule.BibleReadingSchedule.PublicationCode));

                    if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
                    {
                        scheduleStateItem.BibleReadingPublicationName = translation.Name;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "CancelCommand: Error populating BibleReadingPublicationName");
                }
            }

            // Book name
            if (schedule.BibleReadingSchedule.BookNumber > 0 &&
                !string.IsNullOrWhiteSpace(schedule.BibleReadingSchedule.LanguageCode) &&
                !string.IsNullOrWhiteSpace(schedule.BibleReadingSchedule.PublicationCode))
            {
                try
                {
                    var bibleBookService = serviceProvider.GetRequiredService<IBibleBookService>();
                    // Run database operations off UI thread
                    var bookName = await Task.Run(async () =>
                        await bibleBookService.GetBookNameAsync(
                            schedule.BibleReadingSchedule.LanguageCode,
                            schedule.BibleReadingSchedule.PublicationCode,
                            schedule.BibleReadingSchedule.BookNumber));

                    if (!string.IsNullOrWhiteSpace(bookName))
                    {
                        scheduleStateItem.BibleReadingBookName = bookName;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "CancelCommand: Error populating BibleReadingBookName");
                }
            }
        }

        // Populate music display names
        if (schedule.Music != null)
        {
            var music = schedule.Music;

            // Music language name (for vocals)
            if (music.MusicType == MusicType.Vocals &&
                !string.IsNullOrWhiteSpace(music.LanguageCode))
            {
                try
                {
                    // Run database operations off UI thread
                    var languagesDict = await Task.Run(async () =>
                        await mediaService.GetVocalMusicLanguages());
                    if (languagesDict.TryGetValue(music.LanguageCode, out var language))
                    {
                        scheduleStateItem.MusicLanguageName = language.Name;
                    }
                    else
                    {
                        scheduleStateItem.MusicLanguageName = music.LanguageCode;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "CancelCommand: Error populating MusicLanguageName");
                }
            }

            // Music publication name (for vocals)
            if (music.MusicType == MusicType.Vocals &&
                !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                !string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                try
                {
                    // Run database operations off UI thread
                    var releases = await Task.Run(async () =>
                        await mediaService.GetVocalMusicReleases(music.LanguageCode));
                    if (releases.TryGetValue(music.PublicationCode, out var release))
                    {
                        scheduleStateItem.MusicPublicationName = release.Name;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "CancelCommand: Error populating MusicPublicationName");
                }
            }

            // Music track name
            if (music.TrackNumber > 0)
            {
                try
                {
                    string? trackName = null;
                    if (music.MusicType == MusicType.Melodies)
                    {
                        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                        {
                            // Run database operations off UI thread
                            var tracks = await Task.Run(async () =>
                                await mediaService.GetMelodyMusicTracks(music.PublicationCode));
                            if (tracks.TryGetValue(music.TrackNumber, out var track))
                            {
                                // Format melody track title with prefix to match track modal display
                                trackName = $"Melody Number(s) {track.Title}";
                            }
                        }
                    }
                    else if (music.MusicType == MusicType.Vocals)
                    {
                        if (!string.IsNullOrWhiteSpace(music.LanguageCode) && !string.IsNullOrWhiteSpace(music.PublicationCode))
                        {
                            // Run database operations off UI thread
                            var tracks = await Task.Run(async () =>
                                await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode));
                            if (tracks.TryGetValue(music.TrackNumber, out var track))
                            {
                                trackName = track.Title;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(trackName))
                    {
                        scheduleStateItem.MusicTrackName = trackName;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "CancelCommand: Error populating MusicTrackName");
                }
            }
        }
    }

    private async Task ExecuteSaveCommand()
    {
        logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            IsNewSchedule, ScheduleId, Name);

        isSaving = true;

        try
        {
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

    private async Task ValidateNotificationPermissions()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null &&
            currentSchedule.IsEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.WinUI) &&
            !await notificationService.CanScheduleAsync())
        {
            // Update state to disable notifications
            var updatedSchedule = CloneScheduleStateItem(currentSchedule);
            updatedSchedule.IsEnabled = false;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
        }
    }

    private async Task StopPlaybackIfNeeded()
    {
        if (!IsNewSchedule &&
            playbackState.Value.IsPreparingOrPlaying &&
            ScheduleId == playbackState.Value.CurrentScheduleId)
        {
            await playbackService.StopAsync();
        }
    }

    private async Task HandleSaveResult(bool saved)
    {
        if (saved)
        {
            logger.Information("SaveCommand: Save successful, navigating to home. ScheduleId={ScheduleId}", ScheduleId);

            // Clear CurrentSchedule after successful save
            dispatcher.Dispatch(new ResetScheduleStateAction());
            
            // Hide overlay when navigating away after successful save
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

            await Task.Delay(100);
            await navigationService.NavigateToHomeAsync();
        }
        else
        {
            logger.Error("SaveCommand: Save failed, hiding overlay. ScheduleId={ScheduleId}", ScheduleId);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        }

        if (saved && IsEnabled)
        {
            var model = GetModel();
            await popUpService.ShowScheduledNotification(model);
        }
    }

    private async Task ExecuteDeleteCommand()
    {
        if (IsNewSchedule)
        {
            // Hide overlay when navigating away
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            await navigationService.NavigateToHomeAsync();
            return;
        }

        await StopPlaybackIfNeeded();
        await DeleteAsync();
        
        // Hide overlay when navigating away after delete
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        await navigationService.NavigateToHomeAsync();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentScheduleId = stateValue.CurrentSchedule?.Id ?? -1;
        var overlayVisible = stateValue.IsSchedulePageOverlayVisible;

        logger.Debug("OnStateChanged: IsSchedulePageOverlayVisible={OverlayVisible}, CurrentScheduleId={ScheduleId}", 
            overlayVisible, currentScheduleId);

        // Always update overlay visibility when state changes
        // This ensures the UI updates when SetSchedulePageOverlayAction is dispatched
        // Use SetProperty to ensure proper change notification
        var newOverlayVisible = stateValue.IsSchedulePageOverlayVisible;
        if (newOverlayVisible != isSchedulePageOverlayVisible)
        {
            logger.Debug("OnStateChanged: Overlay visibility changed from {OldValue} to {NewValue}", 
                isSchedulePageOverlayVisible, newOverlayVisible);
            
            // Use BeginInvokeOnMainThread to ensure we're on the UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsSchedulePageOverlayVisible = newOverlayVisible;
            });
        }

        // Only call OnCurrentScheduleChanged if the schedule actually changed
        // This prevents infinite loops when the same schedule is already loaded
        if (currentScheduleId != lastScheduleId || !modelInitialized)
        {
            // Notify property changes for properties that read from state
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(DaysOfWeek));
                OnPropertyChanged(nameof(Time));
                OnPropertyChanged(nameof(MusicEnabled));
            });

            // Continue with existing schedule change handling
            OnCurrentScheduleChanged(sender, e);
        }
        else
        {
            // Schedule hasn't changed, just update property notifications
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(DaysOfWeek));
                OnPropertyChanged(nameof(Time));
                OnPropertyChanged(nameof(MusicEnabled));
            });
        }
    }

    private void OnCurrentScheduleChanged(object? sender, EventArgs e)
    {
#if DEBUG
        var handlerStartTime = DateTime.UtcNow;
        logger.Information("[PERF] OnCurrentScheduleChanged: Handler started at {StartTime}", handlerStartTime);
#endif

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
#if DEBUG
        logger.Information("[PERF] OnCurrentScheduleChanged: Processing schedule Id={ScheduleId}, LastScheduleId={LastScheduleId}, ModelInitialized={ModelInitialized}",
            currentScheduleId, lastScheduleId, modelInitialized);
#endif

        if (currentScheduleId == lastScheduleId && modelInitialized)
        {
            if (isSaving)
            {
                logger.Debug("OnCurrentScheduleChanged: Skipping model update during save operation. ScheduleId={ScheduleId}", currentScheduleId);
                return;
            }

            // Only update if there are actual changes to avoid infinite loops
            var hasChanges = HandleScheduleUpdateFromState(stateValue, currentScheduleId);
            
            // Always ensure overlay is hidden if schedule is already loaded
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            
            return;
        }

        LoadScheduleFromState(stateValue, currentScheduleId);
    }

    private bool HandleScheduleUpdateFromState(ApplicationState stateValue, int currentScheduleId)
    {
        var currentSchedule = stateValue.CurrentSchedule;
        bool hasChanges = false;
        
        if (currentSchedule != null)
        {
            // Check if music properties changed
            var musicTypeChanged = lastMusicType != currentSchedule.MusicType;
            var musicTrackChanged = lastMusicTrackNumber != currentSchedule.MusicTrackNumber;
            var musicPublicationChanged = lastMusicPublicationCode != currentSchedule.MusicPublicationCode;
            var musicLanguageChanged = lastMusicLanguageCode != currentSchedule.MusicLanguageCode;
            var musicRepeatChanged = lastMusicRepeat != currentSchedule.MusicRepeat;

            if (musicTypeChanged || musicTrackChanged || musicPublicationChanged || musicLanguageChanged || musicRepeatChanged)
            {
                logger.Debug("HandleScheduleUpdateFromState: Music changed. Type: {OldType} -> {NewType}, Track: {OldTrack} -> {NewTrack}, Publication: {OldPub} -> {NewPub}, Language: {OldLang} -> {NewLang}, Repeat: {OldRepeat} -> {NewRepeat}",
                    lastMusicType, currentSchedule.MusicType,
                    lastMusicTrackNumber, currentSchedule.MusicTrackNumber,
                    lastMusicPublicationCode, currentSchedule.MusicPublicationCode,
                    lastMusicLanguageCode, currentSchedule.MusicLanguageCode,
                    lastMusicRepeat, currentSchedule.MusicRepeat);

                musicUpdated = true;
                hasChanges = true;

                // Update tracking fields
                lastMusicType = currentSchedule.MusicType;
                lastMusicTrackNumber = currentSchedule.MusicTrackNumber;
                lastMusicPublicationCode = currentSchedule.MusicPublicationCode;
                lastMusicLanguageCode = currentSchedule.MusicLanguageCode;
                lastMusicRepeat = currentSchedule.MusicRepeat;
            }
        }

        // Only notify property changes if there were actual changes to avoid infinite loops
        if (hasChanges)
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
        
        return hasChanges;
    }

    /// <summary>
    /// Loads an existing schedule from state without accessing the database.
    /// For existing schedules, all required fields (including display names) are already populated
    /// in the Schedules collection during bootstrap. The schedule is copied from state (immutable copy).
    /// 
    /// Optimized: Data preparation happens off UI thread, only UI updates happen on UI thread.
    /// </summary>
    private void LoadScheduleFromState(ApplicationState stateValue, int currentScheduleId)
    {
        isInitializingNewSchedule = false;
        var currentScheduleItem = stateValue.CurrentSchedule!;

#if DEBUG
        logger.Information("[PERF] OnCurrentScheduleChanged: Preparing data off UI thread for ScheduleId={ScheduleId}", currentScheduleId);
#endif

        // Prepare data structures off UI thread (DeepClone is CPU-intensive JSON serialization)
        _ = Task.Run(async () =>
        {
            try
            {
#if DEBUG
                var dataPrepStartTime = DateTime.UtcNow;
                logger.Information("[PERF] LoadScheduleFromState: Data preparation started at {StartTime}", dataPrepStartTime);
#endif

                // DeepClone uses JSON serialization which is CPU-intensive - do it off UI thread
                var scheduleStateItemSnapshot = currentScheduleItem.DeepClone();
                var isNew = scheduleStateItemSnapshot.Id <= 0;

                // Initialize tracking fields off UI thread (these are just value assignments)
                lastScheduleId = currentScheduleId;
                lastMusicType = scheduleStateItemSnapshot.MusicType;
                lastMusicTrackNumber = scheduleStateItemSnapshot.MusicTrackNumber;
                lastMusicPublicationCode = scheduleStateItemSnapshot.MusicPublicationCode;
                lastMusicLanguageCode = scheduleStateItemSnapshot.MusicLanguageCode;
                lastMusicRepeat = scheduleStateItemSnapshot.MusicRepeat;

                // Reset musicUpdated when loading a schedule (only set to true if music changes after load)
                musicUpdated = false;
                bibleReadingUpdated = false;

#if DEBUG
                var dataPrepElapsed = (DateTime.UtcNow - dataPrepStartTime).TotalMilliseconds;
                logger.Information("[PERF] LoadScheduleFromState: Data preparation completed in {ElapsedMs}ms, IsNew={IsNew}", dataPrepElapsed, isNew);
#endif

                // Capture isNew for closure
                var isNewSchedule = isNew;

                // Now update UI on main thread (only UI-touching operations)
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
#if DEBUG
                        var mainThreadStartTime = DateTime.UtcNow;
                        logger.Information("[PERF] LoadScheduleFromState: Main thread handler started at {StartTime}", mainThreadStartTime);
#endif

                        IsNewSchedule = isNewSchedule;

                        // Properties read from state, so just notify property changes
                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(IsEnabled));
                        OnPropertyChanged(nameof(DaysOfWeek));
                        OnPropertyChanged(nameof(Time));
                        OnPropertyChanged(nameof(MusicEnabled));

                        await CompleteScheduleLoad();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in LoadScheduleFromState main thread handler");
                        IsBusy = false;
                        OnPropertyChanged(nameof(IsBusy));
                        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in LoadScheduleFromState data preparation");
                // Fallback: update UI even if data prep fails
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    OnPropertyChanged(nameof(IsBusy));
                    dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                });
            }
        });
    }

    private async Task CompleteScheduleLoad()
    {
        modelInitialized = true;
        
        // Wait briefly for initial state to settle
        await Task.Delay(50);
        IsBusy = false;
        OnPropertyChanged(nameof(IsBusy));
        
        // Wait for containers to be initialized (with timeout to prevent indefinite wait)
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
        
        // Brief delay for property notifications to process
        await Task.Delay(50);
        
        // Hide overlay after everything is loaded and rendered
        logger.Debug("CompleteScheduleLoad: Hiding schedule page overlay");
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        
        // Brief delay for Fluxor to process the action
        await Task.Delay(50);
        
        // Explicitly notify property change to ensure UI updates
        // We're already on the main thread, so call directly
        OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
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
        logger.Debug("OnCurrentScheduleChanged: Resetting ViewModel for new schedule. Previous ScheduleId={PreviousScheduleId}", ScheduleId);
        modelInitialized = false;
        lastScheduleId = -1;
        IsNewSchedule = false;

        // Reset music tracking fields
        lastMusicType = null;
        lastMusicTrackNumber = null;
        lastMusicPublicationCode = null;
        lastMusicLanguageCode = null;
        lastMusicRepeat = null;
        musicUpdated = false;
        bibleReadingUpdated = false;
    }

    /// <summary>
    /// Initializes a new schedule by accessing the media database to get sample data.
    /// This is the ONLY time we access the media database in ScheduleViewModel.
    /// For existing schedules, we load from state (see LoadScheduleFromState).
    /// 
    /// NOTE: This makes multiple queries to get sample data and populate display names.
    /// This is acceptable for new schedules as it only runs when creating a new schedule.
    /// For existing schedules, display names are already in state (populated during bootstrap).
    /// </summary>
    private void InitializeNewSchedule()
    {
        isInitializingNewSchedule = true;
#if DEBUG
        logger.Information("[PERF] OnCurrentScheduleChanged: Starting new schedule initialization");
#endif
        MainThread.BeginInvokeOnMainThread(() => IsBusy = true);

        Task.Run(async () =>
        {
#if DEBUG
            var getSampleStartTime = DateTime.UtcNow;
            logger.Information("[PERF] OnCurrentScheduleChanged: GetSampleSchedule started at {StartTime}", getSampleStartTime);
#endif

            var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, bibleTranslationService, melodyMusicService);

#if DEBUG
            var getSampleElapsed = (DateTime.UtcNow - getSampleStartTime).TotalMilliseconds;
            logger.Information("[PERF] OnCurrentScheduleChanged: GetSampleSchedule completed in {ElapsedMs}ms", getSampleElapsed);
#endif

            // Map sample schedule to state item
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(sampleSchedule);
            
            // Log the music type to verify it's Melodies (not Vocals)
            logger.Information("OnCurrentScheduleChanged: Mapped sample schedule. MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}",
                scheduleStateItem.MusicType?.ToString() ?? "null",
                scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
                scheduleStateItem.MusicPublicationCode ?? "null");

            // Populate display names before dispatching action
            logger.Debug("OnCurrentScheduleChanged: Populating display names for new schedule");
            await PopulateDisplayNamesAsync(scheduleStateItem, sampleSchedule);
            logger.Debug("OnCurrentScheduleChanged: Display names populated. LanguageName: {LanguageName}, PublicationName: {PublicationName}, BookName: {BookName}",
                scheduleStateItem.BibleReadingLanguageName ?? "null",
                scheduleStateItem.BibleReadingPublicationName ?? "null",
                scheduleStateItem.BibleReadingBookName ?? "null");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var currentState = state.Value;
                if (isInitializingNewSchedule && !modelInitialized && currentState.CurrentSchedule == null)
                {
                    logger.Debug("OnCurrentScheduleChanged: Initializing new schedule. SampleSchedule.Id={SampleScheduleId}", sampleSchedule.Id);

                    // Set modelInitialized BEFORE dispatching to prevent re-entry
                    modelInitialized = true;
                    lastScheduleId = scheduleStateItem.Id;
                    
                    // Dispatch action to set it in state (display names are already populated)
                    dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));

                    IsNewSchedule = true;

                    // Initialize music tracking fields for new schedule
                    lastMusicType = scheduleStateItem.MusicType;
                    lastMusicTrackNumber = scheduleStateItem.MusicTrackNumber;
                    lastMusicPublicationCode = scheduleStateItem.MusicPublicationCode;
                    lastMusicLanguageCode = scheduleStateItem.MusicLanguageCode;
                    lastMusicRepeat = scheduleStateItem.MusicRepeat;
                    musicUpdated = false; // New schedules start with musicUpdated = false
                    bibleReadingUpdated = false; // New schedules start with bibleReadingUpdated = false

                    // Properties will be updated when state changes, so just notify
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(DaysOfWeek));
                    OnPropertyChanged(nameof(Time));
                    OnPropertyChanged(nameof(MusicEnabled));

                    logger.Information("OnCurrentScheduleChanged: New schedule initialized. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}",
                        scheduleStateItem.Id, IsNewSchedule);
                    
                    isInitializingNewSchedule = false;
                    IsBusy = false;
                    OnPropertyChanged(nameof(IsBusy));
                    
                    // Wait for state change to propagate through Fluxor
                    await Task.Delay(100);
                    
                    // Wait for UI thread to process property changes from container view models
                    // The containers update their display text asynchronously via MainThread.BeginInvokeOnMainThread
                    await Task.Delay(300);
                    
                    // Hide overlay after everything is loaded and rendered
                    logger.Debug("Hiding schedule page overlay after new schedule initialization");
                    var beforeState = state.Value.IsSchedulePageOverlayVisible;
                    logger.Debug("Before dispatching SetSchedulePageOverlayAction: IsSchedulePageOverlayVisible={Value}", beforeState);
                    dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                    
                    // Wait for Fluxor to process the action and update state
                    await Task.Delay(150);
                    
                    var afterState = state.Value.IsSchedulePageOverlayVisible;
                    logger.Debug("After dispatching SetSchedulePageOverlayAction: IsSchedulePageOverlayVisible={Value}", afterState);
                    
                    // Explicitly notify property change to ensure UI updates
                    // We're already on the main thread, so call directly
                    logger.Debug("Calling OnPropertyChanged for IsSchedulePageOverlayVisible on main thread");
                    OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
                    
                    // Force a second notification after a small delay to ensure UI updates
                    await Task.Delay(50);
                    OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
                }
                else
                {
                    // Schedule already in state, just ensure overlay is hidden
                    logger.Debug("Schedule already in state, hiding overlay immediately");
                    isInitializingNewSchedule = false;
                    IsBusy = false;
                    OnPropertyChanged(nameof(IsBusy));
                    dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                    
                    // Wait for Fluxor to process the action and update state
                    await Task.Delay(100);
                    
                    // Explicitly notify property change to ensure UI updates
                    // OnStateChanged will also notify, but this ensures it happens on the UI thread
                    OnPropertyChanged(nameof(IsSchedulePageOverlayVisible));
                }
            });
        });
    }

    public ICommand CancelCommand { get; set; } = null!;

    public ICommand SaveCommand { get; set; } = null!;
    public ICommand DeleteCommand { get; set; } = null!;

    private AlarmSchedule GetModel()
    {
        // Build model from current state
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            logger.Warning("GetModel: CurrentSchedule is null, cannot build model");
            // Return a minimal model for save validation
            return new AlarmSchedule { Id = 0 };
        }

        // Map ScheduleStateItem to AlarmSchedule
        var model = mapper.Map<AlarmSchedule>(currentSchedule);

        logger.Debug("GetModel: Built model from state. Model.Id={ModelId}, Model.Name={ModelName}, Model.MusicEnabled={ModelMusicEnabled}, Model.IsEnabled={ModelIsEnabled}, Model.DaysOfWeek={ModelDaysOfWeek}",
            model.Id, model.Name, model.MusicEnabled, model.IsEnabled, model.DaysOfWeek);

        return model;
    }

    private int ScheduleId
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.Id ?? 0;
        }
    }

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }


    public string Name
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.Name ?? string.Empty;
        }
    }

    public bool IsEnabled
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.IsEnabled ?? false;
        }
    }

    public DaysOfWeek DaysOfWeek
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.DaysOfWeek ?? 0;
        }
    }

    public TimeSpan Time
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null)
            {
                return new TimeSpan(currentSchedule.Hour, currentSchedule.Minute, currentSchedule.Second);
            }
            return TimeSpan.Zero;
        }
    }

    public bool MusicEnabled
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.MusicEnabled ?? false;
        }
    }

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
            IsNewSchedule, ScheduleId, Name, modelInitialized);

        if (!await ValidateSavePreconditions())
        {
            return false;
        }

        // For new schedules, ensure IsEnabled is true in state
        if (IsNewSchedule)
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null && !currentSchedule.IsEnabled)
            {
                var updatedSchedule = CloneScheduleStateItem(currentSchedule);
                updatedSchedule.IsEnabled = true;
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                // Wait a bit for state to update
                await Task.Delay(50);
            }
        }

        var model = PrepareModelForSave();
        await DispatchSaveActionAsync(model);
        SetupMediaCache(ScheduleId, isUpdate: !IsNewSchedule);

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

        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            logger.Error("SaveAsync: CurrentSchedule is null. Cannot save.");
            await popUpService.ShowMessage("Schedule data is missing. Please try again.");
            return false;
        }

        if (!IsNewSchedule && ScheduleId <= 0)
        {
            logger.Error("SaveAsync: Invalid ScheduleId for existing schedule. ScheduleId={ScheduleId}", ScheduleId);
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
        logger.Information("PrepareModelForSave: Starting. musicUpdated={MusicUpdated}, IsNewSchedule={IsNewSchedule}", musicUpdated, IsNewSchedule);

        var currentSchedule = state.Value.CurrentSchedule;
        logger.Information("PrepareModelForSave: CurrentSchedule state - MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicId={MusicId}",
            currentSchedule?.MusicType?.ToString() ?? "null",
            currentSchedule?.MusicTrackNumber?.ToString() ?? "null",
            currentSchedule?.MusicPublicationCode ?? "null",
            currentSchedule?.MusicLanguageCode ?? "null",
            currentSchedule?.MusicId?.ToString() ?? "null");

        var model = GetModel();

        logger.Information("PrepareModelForSave: After GetModel() - model.Music={HasMusic}, model.Music?.MusicType={MusicType}, model.Music?.TrackNumber={TrackNumber}, model.Music?.PublicationCode={PublicationCode}, model.Music?.LanguageCode={LanguageCode}",
            model.Music != null ? "not null" : "null",
            model.Music?.MusicType.ToString() ?? "null",
            model.Music?.TrackNumber.ToString() ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

        EnsureDefaultPublicationCode(model);

        // If music was updated, ensure model.Music has the correct music type from state
        // This is critical for music type changes (e.g., Melodies -> Vocals)
        if (musicUpdated)
        {
            logger.Information("PrepareModelForSave: musicUpdated=true, updating model.Music from state");
            if (currentSchedule != null &&
                currentSchedule.MusicType.HasValue &&
                currentSchedule.MusicTrackNumber.HasValue &&
                currentSchedule.MusicTrackNumber.Value > 0)
            {
                // Ensure model.Music exists and has correct properties from state
                if (model.Music == null)
                {
                    logger.Information("PrepareModelForSave: model.Music is null, creating new AlarmMusic from state");
                    model.Music = new AlarmMusic
                    {
                        Id = currentSchedule.MusicId ?? 0,
                        MusicType = currentSchedule.MusicType.Value,
                        PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                        LanguageCode = currentSchedule.MusicLanguageCode,
                        TrackNumber = currentSchedule.MusicTrackNumber.Value,
                        Repeat = currentSchedule.MusicRepeat ?? false,
                        AlarmScheduleId = model.Id
                    };
                    logger.Information("PrepareModelForSave: Created model.Music from state. MusicType={MusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        model.Music.MusicType, model.Music.TrackNumber, model.Music.PublicationCode, model.Music.LanguageCode);
                }
                else
                {
                    var oldMusicType = model.Music.MusicType;
                    var oldTrackNumber = model.Music.TrackNumber;
                    // Update existing model.Music with correct properties from state
                    model.Music.MusicType = currentSchedule.MusicType.Value;
                    model.Music.PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty;
                    model.Music.LanguageCode = currentSchedule.MusicLanguageCode;
                    model.Music.TrackNumber = currentSchedule.MusicTrackNumber.Value;
                    model.Music.Repeat = currentSchedule.MusicRepeat ?? false;
                    if (currentSchedule.MusicId.HasValue)
                    {
                        model.Music.Id = currentSchedule.MusicId.Value;
                    }
                    logger.Information("PrepareModelForSave: Updated model.Music from state. Old MusicType={OldMusicType} -> New MusicType={NewMusicType}, Old TrackNumber={OldTrackNumber} -> New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        oldMusicType, model.Music.MusicType, oldTrackNumber, model.Music.TrackNumber, model.Music.PublicationCode, model.Music.LanguageCode);
                }
            }
            else
            {
                logger.Warning("PrepareModelForSave: musicUpdated=true but CurrentSchedule music properties are invalid. MusicType={MusicType}, MusicTrackNumber={TrackNumber}",
                    currentSchedule?.MusicType?.ToString() ?? "null",
                    currentSchedule?.MusicTrackNumber?.ToString() ?? "null");
            }
        }
        else
        {
            logger.Information("PrepareModelForSave: musicUpdated=false, skipping music update");
        }

        ClearUnchangedMusic(model);

        // Ensure MusicEnabled is set from CurrentSchedule state (in case it was toggled)
        if (currentSchedule != null)
        {
            model.MusicEnabled = currentSchedule.MusicEnabled;
            logger.Information("PrepareModelForSave: Set model.MusicEnabled={MusicEnabled} from CurrentSchedule state",
                model.MusicEnabled);
        }

        logger.Information("PrepareModelForSave: Final model - Model.Id={ModelId}, Model.Name={ModelName}, HasMusic={HasMusic}, MusicEnabled={MusicEnabled}, MusicType={MusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            model.Id, model.Name, model.Music != null, model.MusicEnabled,
            model.Music?.MusicType.ToString() ?? "null",
            model.Music?.TrackNumber.ToString() ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

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

    private async Task DispatchSaveActionAsync(AlarmSchedule model)
    {
        logger.Information("DispatchSaveActionAsync: Starting. IsNewSchedule={IsNewSchedule}, MusicUpdated={MusicUpdated}, BibleReadingUpdated={BibleReadingUpdated}",
            IsNewSchedule, musicUpdated, bibleReadingUpdated);

        logger.Information("DispatchSaveActionAsync: Model before mapping - MusicEnabled={MusicEnabled}, MusicType={MusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            model.MusicEnabled,
            model.Music?.MusicType.ToString() ?? "null",
            model.Music?.TrackNumber.ToString() ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

        var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);

        // Ensure MusicEnabled and all display names are set from CurrentSchedule state
        // Display names are populated during initialization and when user selects items, but are not stored in DB
        // We need to preserve them from state so they appear correctly on the home page after save
        var currentScheduleForSave = state.Value.CurrentSchedule;
        if (currentScheduleForSave != null)
        {
            scheduleStateItem.MusicEnabled = currentScheduleForSave.MusicEnabled;
            logger.Information("DispatchSaveActionAsync: Set scheduleStateItem.MusicEnabled={MusicEnabled} from CurrentSchedule state",
                scheduleStateItem.MusicEnabled);

            // Preserve all display names from CurrentSchedule state
            // These are populated during initialization and when user selects items
            // Display names are not stored in DB, so we must preserve them from state
            scheduleStateItem.BibleReadingLanguageName = currentScheduleForSave.BibleReadingLanguageName;
            scheduleStateItem.BibleReadingPublicationName = currentScheduleForSave.BibleReadingPublicationName;
            scheduleStateItem.BibleReadingBookName = currentScheduleForSave.BibleReadingBookName;

            // Preserve music display names as well (will be overridden later if musicUpdated is true)
            scheduleStateItem.MusicLanguageName = currentScheduleForSave.MusicLanguageName;
            scheduleStateItem.MusicPublicationName = currentScheduleForSave.MusicPublicationName;
            scheduleStateItem.MusicTrackName = currentScheduleForSave.MusicTrackName;

            logger.Information("DispatchSaveActionAsync: Preserved display names from CurrentSchedule state. BibleReadingLanguageName={LanguageName}, BibleReadingPublicationName={PublicationName}, MusicTrackName={MusicTrackName}",
                scheduleStateItem.BibleReadingLanguageName ?? "null",
                scheduleStateItem.BibleReadingPublicationName ?? "null",
                scheduleStateItem.MusicTrackName ?? "null");
        }

        logger.Information("DispatchSaveActionAsync: After mapping - scheduleStateItem.MusicEnabled={MusicEnabled}, scheduleStateItem.MusicType={MusicType}, scheduleStateItem.MusicTrackNumber={TrackNumber}, scheduleStateItem.MusicPublicationCode={PublicationCode}, scheduleStateItem.MusicLanguageCode={LanguageCode}",
            scheduleStateItem.MusicEnabled,
            scheduleStateItem.MusicType?.ToString() ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null");

        // If music was updated, always use music properties from CurrentSchedule state
        // This ensures music type changes (e.g., Melodies -> Vocals) are preserved
        if (musicUpdated)
        {
            logger.Information("DispatchSaveActionAsync: musicUpdated=true, overriding with CurrentSchedule state");
            var currentSchedule = state.Value.CurrentSchedule;
            logger.Information("DispatchSaveActionAsync: CurrentSchedule state - MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicRepeat={MusicRepeat}, MusicId={MusicId}",
                currentSchedule?.MusicType?.ToString() ?? "null",
                currentSchedule?.MusicTrackNumber?.ToString() ?? "null",
                currentSchedule?.MusicPublicationCode ?? "null",
                currentSchedule?.MusicLanguageCode ?? "null",
                currentSchedule?.MusicRepeat?.ToString() ?? "null",
                currentSchedule?.MusicId?.ToString() ?? "null");

            if (currentSchedule != null &&
                currentSchedule.MusicType.HasValue &&
                currentSchedule.MusicTrackNumber.HasValue &&
                currentSchedule.MusicTrackNumber.Value > 0)
            {
                var oldMusicType = scheduleStateItem.MusicType;
                // Use music properties from state (includes music type changes)
                scheduleStateItem.MusicType = currentSchedule.MusicType;
                scheduleStateItem.MusicTrackNumber = currentSchedule.MusicTrackNumber;
                scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
                scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
                scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
                scheduleStateItem.MusicId = currentSchedule.MusicId;
                // Preserve display names as well
                scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
                scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
                scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

                logger.Information("DispatchSaveActionAsync: Overrode music properties from CurrentSchedule state. Old MusicType={OldMusicType} -> New MusicType={NewMusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}, Repeat={Repeat}",
                    oldMusicType?.ToString() ?? "null", scheduleStateItem.MusicType?.ToString() ?? "null",
                    scheduleStateItem.MusicTrackNumber, scheduleStateItem.MusicPublicationCode, scheduleStateItem.MusicLanguageCode, scheduleStateItem.MusicRepeat);
            }
            else
            {
                logger.Warning("DispatchSaveActionAsync: musicUpdated=true but CurrentSchedule music properties are invalid. MusicType={MusicType}, MusicTrackNumber={TrackNumber}",
                    currentSchedule?.MusicType?.ToString() ?? "null",
                    currentSchedule?.MusicTrackNumber?.ToString() ?? "null");
            }
        }
        // Populate default music properties when music is disabled
        // This ensures music properties are set to default (same as sample schedule) when music is disabled
        // NOTE: Only use state - do NOT query database here (DB queries only during bootstrap and new schedule)
        else if (!musicUpdated && (!model.MusicEnabled || model.Music == null))
        {
            // Check if music properties are missing or need to be set to default
            var needsDefaultMusic = !scheduleStateItem.MusicType.HasValue ||
                                   !scheduleStateItem.MusicTrackNumber.HasValue ||
                                   scheduleStateItem.MusicTrackNumber.Value <= 0;

            if (needsDefaultMusic)
            {
                // Get music properties from current schedule state (populated during bootstrap)
                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule != null &&
                    currentSchedule.MusicType.HasValue &&
                    currentSchedule.MusicTrackNumber.HasValue &&
                    currentSchedule.MusicTrackNumber.Value > 0)
                {
                    // Use music properties from state (already populated during bootstrap)
                    scheduleStateItem.MusicType = currentSchedule.MusicType;
                    scheduleStateItem.MusicTrackNumber = currentSchedule.MusicTrackNumber;
                    scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
                    scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
                    scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
                    scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

                    logger.Debug("SaveAsync: Using music properties from CurrentSchedule state (no DB query)");
                }
                else
                {
                    logger.Warning("SaveAsync: Music properties not in state and not querying DB. Music properties will be missing.");
                }
            }
        }
        else if (!IsNewSchedule && !musicUpdated)
        {
            // For existing schedules where music is enabled but not updated, preserve existing music properties
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null && model.Music == null)
            {
                // Preserve music properties from current schedule state
                scheduleStateItem.MusicType = currentSchedule.MusicType;
                scheduleStateItem.MusicTrackNumber = currentSchedule.MusicTrackNumber;
                scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
                scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
                scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
                scheduleStateItem.MusicId = currentSchedule.MusicId;
                // Preserve display names as well
                scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
                scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
                scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

                logger.Debug("SaveAsync: Preserved music properties from current schedule state. MusicType={MusicType}, TrackNumber={TrackNumber}",
                    scheduleStateItem.MusicType, scheduleStateItem.MusicTrackNumber);
            }
        }

        logger.Information("DispatchSaveActionAsync: Final scheduleStateItem before dispatch - MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicId={MusicId}",
            scheduleStateItem.MusicType?.ToString() ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicId?.ToString() ?? "null");

        if (IsNewSchedule)
        {
            logger.Information("DispatchSaveActionAsync: Dispatching CreateScheduleAction");
            dispatcher.Dispatch(new CreateScheduleAction(scheduleStateItem, musicUpdated, bibleReadingUpdated));
        }
        else
        {
            logger.Information("DispatchSaveActionAsync: Dispatching UpdateScheduleFromViewModelAction with shouldSave: true");
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, musicUpdated, bibleReadingUpdated, shouldSave: true));
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
        if (ScheduleId <= 0)
        {
            return;
        }

        // Check if this is the last schedule - prevent deletion if it is
        var scheduleCount = state.Value.Schedules?.Count ?? 0;
        if (scheduleCount <= 1)
        {
            logger.Warning("Cannot delete schedule {ScheduleId} - it is the last schedule", ScheduleId);
            await popUpService.ShowMessage("Cannot delete last schedule");
            return;
        }

        logger.Information("DeleteAsync: Dispatching DeleteScheduleAction for ScheduleId={ScheduleId}", ScheduleId);

        // Dispatch action with schedule ID (following Fluxor best practices)
        dispatcher.Dispatch(new DeleteScheduleAction(ScheduleId));

        // Note: The Effect will handle the actual deletion and dispatch success/failure
        // Media cache deletion is handled in the Effect
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

    private static ScheduleStateItem CloneScheduleStateItem(ScheduleStateItem source)
    {
        return new ScheduleStateItem
        {
            Id = source.Id,
            Name = source.Name,
            IsEnabled = source.IsEnabled,
            Hour = source.Hour,
            Minute = source.Minute,
            Second = source.Second,
            DaysOfWeek = source.DaysOfWeek,
            NotificationEnabled = source.NotificationEnabled,
            MusicEnabled = source.MusicEnabled,
            SnoozeMinutes = source.SnoozeMinutes,
            NumberOfChaptersToRead = source.NumberOfChaptersToRead,
            AlwaysPlayFromStart = source.AlwaysPlayFromStart,
            CurrentPlayItem = source.CurrentPlayItem,
            LatestAlarmNotificationId = source.LatestAlarmNotificationId,
            BibleReadingScheduleId = source.BibleReadingScheduleId,
            BibleReadingLanguageCode = source.BibleReadingLanguageCode,
            BibleReadingPublicationCode = source.BibleReadingPublicationCode,
            BibleReadingBookNumber = source.BibleReadingBookNumber,
            BibleReadingChapterNumber = source.BibleReadingChapterNumber,
            BibleReadingFinishedDuration = source.BibleReadingFinishedDuration,
            MusicId = source.MusicId,
            MusicType = source.MusicType,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackNumber = source.MusicTrackNumber,
            MusicRepeat = source.MusicRepeat,
            BibleReadingLanguageName = source.BibleReadingLanguageName,
            BibleReadingPublicationName = source.BibleReadingPublicationName,
            BibleReadingBookName = source.BibleReadingBookName,
            MusicLanguageName = source.MusicLanguageName,
            MusicPublicationName = source.MusicPublicationName,
            MusicTrackName = source.MusicTrackName
        };
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        
        // Hide overlay when ViewModel is disposed
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
    }
}
