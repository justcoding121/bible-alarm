#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.ViewModels.Services.MusicSelection;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class MusicSelectionContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Helper classes for modular functionality
    private readonly MusicCommandInitializer commandInitializer;
    private readonly MusicDisplayTextProvider displayTextProvider;
    private readonly MusicPropertyNotifier propertyNotifier;

    private int scheduleId;
    private bool isNewSchedule;
    private bool musicUpdated;
    private AlarmMusic? music;
    private AlarmMusic? lastMusic;
    private AlarmSchedule? model;

    private bool isUpdatingFromState;
    private bool? pendingMusicEnabled; // Optimistic update value
    private bool? initialMusicEnabledOnPageLoad; // Track MusicEnabled state when schedule page was first opened

    // Track last values from CurrentSchedule to detect changes
    private MusicType? lastScheduleMusicType;
    private int? lastScheduleMusicTrackNumber;
    private string? lastScheduleMusicPublicationCode;
    private string? lastScheduleMusicLanguageCode;
    private bool lastScheduleMusicRepeat;
    private bool? lastMusicEnabled; // Track previous MusicEnabled state

    // Signal to View that it should scroll to bottom
    private bool shouldScrollToBottom;
    public bool ShouldScrollToBottom
    {
        get => shouldScrollToBottom;
        set => SetProperty(ref shouldScrollToBottom, value);
    }

    public MusicSelectionContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        IServiceProvider serviceProvider,
        IToastService toastService)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;

        // Initialize helper classes
        commandInitializer = new MusicCommandInitializer(
            logger, navigationService, scheduleSelectionService, state, dispatcher, mapper, serviceProvider, toastService);
        displayTextProvider = new MusicDisplayTextProvider(state);
        propertyNotifier = new MusicPropertyNotifier(propertyName => OnPropertyChanged(propertyName), displayTextProvider);

        state.StateChanged += OnStateChanged;
        InitializeCommands();
        InitializeFromState();
    }

    private void InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            scheduleId = currentSchedule.Id;
            isNewSchedule = currentSchedule.Id <= 0;

            // Track initial MusicEnabled state when schedule page is first opened
            // This is used to determine if we should reset to default music on first enable
            if (!initialMusicEnabledOnPageLoad.HasValue)
            {
                initialMusicEnabledOnPageLoad = currentSchedule.MusicEnabled;
                logger.Information("MusicSelectionContainerViewModel: InitializeFromState - Tracked initial MusicEnabled={MusicEnabled} for schedule {ScheduleId}",
                    initialMusicEnabledOnPageLoad.Value, scheduleId);
            }

            // Initialize last values from CurrentSchedule
            lastScheduleMusicType = currentSchedule.MusicType;
            lastScheduleMusicTrackNumber = currentSchedule.MusicTrackNumber;
            lastScheduleMusicPublicationCode = currentSchedule.MusicPublicationCode;
            lastScheduleMusicLanguageCode = currentSchedule.MusicLanguageCode;
            lastScheduleMusicRepeat = currentSchedule.MusicRepeat ?? false;
            lastMusicEnabled = currentSchedule.MusicEnabled;

            // Batch property notifications to reduce UI thread work
            propertyNotifier.NotifyAllMusicPropertiesChanged();

            // Initialize track name cache from state if available (populated during bootstrap)
            // NOTE: Do NOT query database here - track names should be in state from bootstrap
            if (currentSchedule.MusicType.HasValue &&
                currentSchedule.MusicTrackNumber.HasValue &&
                currentSchedule.MusicTrackNumber.Value > 0)
            {
                // If MusicTrackName is already in state (from bootstrap), use it immediately
                if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
                {
                    displayTextProvider.UpdateTrackCache(
                        currentSchedule.MusicTrackName,
                        currentSchedule.MusicTrackNumber,
                        currentSchedule.MusicPublicationCode,
                        currentSchedule.MusicLanguageCode,
                        currentSchedule.MusicType);
                }
            }
        }
    }

    private void InitializeCommands()
    {
        SelectMusicCommand = commandInitializer.CreateSelectMusicCommand(
            () => music, m => music = m, scheduleId, isNewSchedule, musicUpdated);
        SelectMusicTypeCommand = commandInitializer.CreateSelectMusicTypeCommand(
            () => music, m => music = m, scheduleId, isNewSchedule, musicUpdated);
        SelectSongBookCommand = commandInitializer.CreateSelectSongBookCommand(
            () => music, m => music = m, scheduleId, isNewSchedule, musicUpdated);
        SelectTrackCommand = commandInitializer.CreateSelectTrackCommand(
            () => music, m => music = m, scheduleId, isNewSchedule, musicUpdated);
        SelectMusicLanguageCommand = commandInitializer.CreateSelectMusicLanguageCommand(
            () => music, m => music = m, scheduleId, isNewSchedule, musicUpdated);
        ToggleRepeatCommand = commandInitializer.CreateToggleRepeatCommand();
    }

    public void SetModel(AlarmSchedule model)
    {
        this.model = model;
    }

    public void SetMusicUpdated(bool musicUpdated)
    {
        this.musicUpdated = musicUpdated;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;

        // Initialize if schedule ID changed (new schedule opened)
        if (currentSchedule != null && currentSchedule.Id != scheduleId)
        {
            // Reset initial MusicEnabled tracking when a new schedule is opened
            initialMusicEnabledOnPageLoad = null;
            InitializeFromState();
        }

        // Check if MusicEnabled changed in state
        if (currentSchedule != null)
        {
            var stateMusicEnabled = currentSchedule.MusicEnabled;
            var previousMusicEnabled = lastMusicEnabled ?? false;

            // Clear pending value since state has been updated
            if (pendingMusicEnabled.HasValue && pendingMusicEnabled.Value == stateMusicEnabled)
            {
                pendingMusicEnabled = null; // State now matches, clear pending
            }

            // Check if MusicEnabled changed by comparing with tracked previous value
            if (lastMusicEnabled != stateMusicEnabled)
            {
                // State has a different value, update tracked value and notify
                isUpdatingFromState = true;
                try
                {
                    lastMusicEnabled = stateMusicEnabled;
                    pendingMusicEnabled = null; // Clear pending when updating from state
                    OnPropertyChanged(nameof(MusicEnabled));

                    // If music was just enabled, update cache from state
                    // NOTE: Loading default music from DB is handled in MusicEnabled setter
                    if (stateMusicEnabled && !previousMusicEnabled)
                    {
                        // Update lastScheduleMusicType to ensure change detection works
                        if (currentSchedule.MusicType.HasValue)
                        {
                            lastScheduleMusicType = currentSchedule.MusicType;
                        }

                        // Notify MusicTypeDisplayText on main thread with delay to ensure content is visible first
                        // This is critical for iOS - bindings are evaluated when content becomes visible
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            // Wait a bit to ensure CollapsibleContent is visible before notifying
                            OnPropertyChanged(nameof(MusicTypeDisplayText));
                            OnPropertyChanged(nameof(IsMusicLanguageVisible));
                            OnPropertyChanged(nameof(IsSongBookVisible));
                        });

                        // Check if MusicTrackName is already in state (from bootstrap or from DB load in setter)
                        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
                        {
                            // Update cache and notify
                            displayTextProvider.UpdateTrackCache(
                                currentSchedule.MusicTrackName,
                                currentSchedule.MusicTrackNumber,
                                currentSchedule.MusicPublicationCode,
                                currentSchedule.MusicLanguageCode,
                                currentSchedule.MusicType);
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                OnPropertyChanged(nameof(TrackDisplayText));
                            });
                        }
                        else
                        {
                            // Track name not in state yet - wait for MusicEnabled setter to load it from DB
                            // Just notify property change to trigger UI update
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                OnPropertyChanged(nameof(TrackDisplayText));
                            });
                        }
                    }
                }
                finally
                {
                    isUpdatingFromState = false;
                }
            }
        }

        // Check if CurrentSchedule.MusicType or MusicRepeat changed
        if (currentSchedule != null)
        {
            var scheduleMusicType = currentSchedule.MusicType;
            var scheduleMusicTrackNumber = currentSchedule.MusicTrackNumber;
            var scheduleMusicPublicationCode = currentSchedule.MusicPublicationCode;
            var scheduleMusicLanguageCode = currentSchedule.MusicLanguageCode;
            var scheduleMusicRepeat = currentSchedule.MusicRepeat ?? false;

            // Check if music type changed in CurrentSchedule (this happens when effect syncs CurrentMusic to CurrentSchedule)
            var musicTypeChanged = lastScheduleMusicType != scheduleMusicType;
            var languageCodeChanged = lastScheduleMusicLanguageCode != scheduleMusicLanguageCode;
            var publicationCodeChanged = lastScheduleMusicPublicationCode != scheduleMusicPublicationCode;
            var trackNumberChanged = lastScheduleMusicTrackNumber != scheduleMusicTrackNumber;
            var repeatChanged = lastScheduleMusicRepeat != scheduleMusicRepeat;

            // Determine which properties need to be notified (cascading logic)
            var notifyMusicType = musicTypeChanged;
            var notifyLanguage = musicTypeChanged || languageCodeChanged;
            var notifySongBook = musicTypeChanged || languageCodeChanged || publicationCodeChanged;
            var notifyTrack = musicTypeChanged || languageCodeChanged || publicationCodeChanged || trackNumberChanged;

            if (notifyMusicType || notifyLanguage || notifySongBook || notifyTrack || repeatChanged)
            {
                logger.Debug("MusicSelectionContainerViewModel: CurrentSchedule music changed. MusicType: {OldType} -> {NewType}, LanguageCode: {OldLang} -> {NewLang}, PublicationCode: {OldPub} -> {NewPub}, TrackNumber: {OldTrack} -> {NewTrack}",
                    lastScheduleMusicType, scheduleMusicType,
                    lastScheduleMusicLanguageCode, scheduleMusicLanguageCode,
                    lastScheduleMusicPublicationCode, scheduleMusicPublicationCode,
                    lastScheduleMusicTrackNumber, scheduleMusicTrackNumber);

                // Update last values
                lastScheduleMusicType = scheduleMusicType;
                lastScheduleMusicTrackNumber = scheduleMusicTrackNumber;
                lastScheduleMusicPublicationCode = scheduleMusicPublicationCode;
                lastScheduleMusicLanguageCode = scheduleMusicLanguageCode;
                lastScheduleMusicRepeat = scheduleMusicRepeat;

                // Trigger property change notifications with cascading logic
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var currentSchedule = state.Value.CurrentSchedule;
                    propertyNotifier.NotifyPropertiesChanged(
                        notifyMusicType,
                        notifyLanguage,
                        notifySongBook,
                        notifyTrack,
                        repeatChanged,
                        scheduleMusicType,
                        shouldScroll => { if (currentSchedule?.MusicEnabled == true) ShouldScrollToBottom = shouldScroll; });
                });
            }
        }

        if (stateValue.CurrentMusic == null)
        {
            return;
        }

        // Check if the music actually changed by comparing properties
        var newMusicItem = stateValue.CurrentMusic;
        var hasChanged = lastMusic == null ||
                        music == null ||
                        lastMusic.LanguageCode != newMusicItem.LanguageCode ||
                        lastMusic.PublicationCode != newMusicItem.PublicationCode ||
                        lastMusic.MusicType != newMusicItem.MusicType ||
                        lastMusic.TrackNumber != newMusicItem.TrackNumber ||
                        (music != null &&
                         (music.TrackNumber != newMusicItem.TrackNumber ||
                          music.MusicType != newMusicItem.MusicType ||
                          music.LanguageCode != newMusicItem.LanguageCode ||
                          music.PublicationCode != newMusicItem.PublicationCode));

        if (!hasChanged)
        {
            return;
        }

        // Map DTO to entity
        var newMusic = mapper.Map<AlarmMusic>(newMusicItem);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            music = newMusic;
            lastMusic = newMusic;
            musicUpdated = true;

            // Determine what changed to trigger cascading notifications
            var musicTypeChanged = music?.MusicType != newMusic.MusicType;
            var languageCodeChanged = music?.LanguageCode != newMusic.LanguageCode;
            var publicationCodeChanged = music?.PublicationCode != newMusic.PublicationCode;
            var trackNumberChanged = music?.TrackNumber != newMusic.TrackNumber;

            // Trigger cascading property change notifications
            MainThread.BeginInvokeOnMainThread(() =>
            {
                propertyNotifier.NotifyPropertiesChanged(
                    musicTypeChanged,
                    languageCodeChanged,
                    publicationCodeChanged,
                    trackNumberChanged,
                    false,
                    newMusic.MusicType);
            });
        });
    }

    public ICommand SelectMusicCommand { get; private set; } = null!;
    public ICommand SelectMusicTypeCommand { get; private set; } = null!;
    public ICommand SelectMusicLanguageCommand { get; private set; } = null!;
    public ICommand SelectSongBookCommand { get; private set; } = null!;
    public ICommand SelectTrackCommand { get; private set; } = null!;
    public ICommand ToggleRepeatCommand { get; private set; } = null!;

    public bool MusicEnabled
    {
        get
        {
            // Return pending value if set (optimistic update), otherwise read from state
            if (pendingMusicEnabled.HasValue)
            {
                return pendingMusicEnabled.Value;
            }
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.MusicEnabled ?? false;
        }
        set
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning("MusicEnabled setter: CurrentSchedule is null, cannot update");
                return;
            }

            // Check if the value is actually different from the current state
            var currentValue = MusicEnabled;
            if (currentValue == value)
            {
                // Value hasn't changed, don't dispatch
                logger.Debug("MusicEnabled setter: Value unchanged ({Value}), skipping", value);
                return;
            }

            // Prevent dispatching if this update is coming from state (not user interaction)
            if (isUpdatingFromState)
            {
                logger.Debug("MusicEnabled setter: Update from state, skipping dispatch");
                if (model != null)
                {
                    model.MusicEnabled = value;
                }
                pendingMusicEnabled = null; // Clear pending when updating from state
                OnPropertyChanged();
                return;
            }

            logger.Debug("MusicEnabled setter: Setting to {Value} (was {CurrentValue})", value, currentValue);

            // Set optimistic update value immediately
            pendingMusicEnabled = value;
            if (model != null)
            {
                model.MusicEnabled = value;
            }

            // Trigger PropertyChanged immediately to update UI
            OnPropertyChanged(nameof(MusicEnabled));

            // Signal to scroll to bottom when user enables music
            if (value && !currentValue)
            {
                ShouldScrollToBottom = true;
            }

            // Update state (will clear pendingMusicEnabled when state updates)
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(currentSchedule.DeepClone());
            scheduleStateItem.MusicEnabled = value;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));

            // If enabling music, only reset to default music if:
            // 1. Music was disabled when schedule page was first opened (initialMusicEnabledOnPageLoad == false)
            // 2. This is the first time enabling it on this page load (value && !currentValue)
            // On subsequent enable/disable cycles, preserve whatever music was selected
            if (value && !currentValue)
            {
                var shouldResetToDefault = initialMusicEnabledOnPageLoad.HasValue &&
                                          initialMusicEnabledOnPageLoad.Value == false;

                if (shouldResetToDefault)
                {
                    // This is the first enable after opening a schedule with music disabled
                    // Reset to default music (same as new schedule)
                    // This is the ONLY place we query DB when enabling music on existing schedule
                    Task.Run(async () =>
                    {
                        try
                        {
                            const string DefaultPublicationCode = "iam";
                            var melodyMusicService = serviceProvider.GetRequiredService<IMelodyMusicService>();

                            logger.Information("MusicEnabled: First enable after opening schedule with music disabled, resetting to default music (same as new schedule)");

                            // Get default music from DB (same as sample schedule)
                            var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(DefaultPublicationCode);

                            if (melodyMusic != null && melodyMusic.Tracks != null && melodyMusic.Tracks.Count > 0)
                            {
                                // Select a random track (same as sample schedule)
                                var randomTrack = melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];

                                // Get the latest state to ensure MusicEnabled is preserved
                                var latestSchedule = state.Value.CurrentSchedule;
                                if (latestSchedule == null)
                                {
                                    logger.Warning("MusicEnabled: CurrentSchedule is null when resetting to default music");
                                    return;
                                }

                                // Update state with default music properties (reset to default)
                                // IMPORTANT: Preserve MusicEnabled from the latest state
                                var scheduleStateItem = mapper.Map<ScheduleStateItem>(latestSchedule.DeepClone());
                                scheduleStateItem.MusicEnabled = true; // Ensure MusicEnabled is true when resetting
                                scheduleStateItem.MusicType = MusicType.Melodies;
                                scheduleStateItem.MusicPublicationCode = DefaultPublicationCode;
                                scheduleStateItem.MusicLanguageCode = null;
                                scheduleStateItem.MusicTrackNumber = randomTrack.Number;
                                scheduleStateItem.MusicRepeat = false;
                                scheduleStateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";

                                logger.Information("MusicEnabled: Resetting to default music. MusicEnabled={MusicEnabled}, MusicType={MusicType}, TrackNumber={TrackNumber}",
                                    scheduleStateItem.MusicEnabled, scheduleStateItem.MusicType, scheduleStateItem.MusicTrackNumber);

                                // Update state
                                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));

                                logger.Information("MusicEnabled: Reset to default music. MusicType=Melodies, TrackNumber={TrackNumber}, TrackName={TrackName}",
                                    randomTrack.Number, scheduleStateItem.MusicTrackName);
                            }
                            else
                            {
                                logger.Warning("MusicEnabled: Could not load default music from DB. Melody music or tracks not found.");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "MusicEnabled: Error loading default music from DB");
                        }
                    });
                }
                else
                {
                    // Subsequent enable or music was already enabled on page load
                    // Preserve existing music selection
                    logger.Information("MusicEnabled: Re-enabling music, preserving existing music selection. InitialMusicEnabledOnPageLoad={InitialMusicEnabled}",
                        initialMusicEnabledOnPageLoad?.ToString() ?? "null");
                }
            }
        }
    }

    public string MusicTypeDisplayText => displayTextProvider.GetMusicTypeDisplayText();
    public bool IsSongBookVisible => displayTextProvider.GetIsSongBookVisible();
    public bool IsMusicLanguageVisible => displayTextProvider.GetIsMusicLanguageVisible();
    public string MusicLanguageDisplayText => displayTextProvider.GetMusicLanguageDisplayText();
    public string SongBookDisplayText => displayTextProvider.GetSongBookDisplayText();
    public async Task<string> GetSongBookDisplayTextAsync() => await displayTextProvider.GetSongBookDisplayTextAsync();
    public string TrackDisplayText => displayTextProvider.GetTrackDisplayText();
    public async Task<string> GetTrackDisplayTextAsync() => await displayTextProvider.GetTrackDisplayTextAsync();
    public bool IsRepeatEnabled => displayTextProvider.GetIsRepeatEnabled();
    public bool HasTrackSelected => displayTextProvider.GetHasTrackSelected();

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

