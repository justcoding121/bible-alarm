#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

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
    private readonly MusicEnabledHandler musicEnabledHandler;
    private readonly MusicStateTracker stateTracker;
    private readonly MusicStateInitializer stateInitializer;
    private readonly MusicStateChangeHandler stateChangeHandler;

    private int scheduleId;
    private bool isNewSchedule;
    private bool isProcessingStateChange;
    // Track MusicEnabled state when schedule page was first opened
    private bool? initialMusicEnabledOnPageLoad;

    // State holder for mutable state (allows use in lambdas without ref parameters)
    private readonly MusicStateHolder stateHolder = new();

    // Signal to View that it should scroll to bottom
    private bool shouldScrollToBottom;

    // Cached selectability flags (updated when state changes)
    private bool isMusicLanguageSelectable = false;
    private bool isSongPublicationSelectable = false;
    private bool isMusicSectionSelectable = false;
    private bool isMusicTrackSelectable = false;
    private string? lastSelectabilitySignature;
    public bool ShouldScrollToBottom
    {
        get => shouldScrollToBottom;
        set => SetProperty(ref shouldScrollToBottom, value);
    }

    /// <summary>
    /// Gets whether the music selection container should be visible.
    /// Returns false when the category is "Music", true otherwise.
    /// This property is computed from the current schedule's category.
    /// </summary>
    public bool IsMusicSelectionVisible
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                // Hide container when no schedule is active (e.g., during navigation)
                return false;
            }

            var categoryName = currentSchedule.BiblePublicationCategoryName;
            var isMusicCategory = !string.IsNullOrWhiteSpace(categoryName) &&
                                  string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase);
            return !isMusicCategory;
        }
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
        displayTextProvider = new MusicDisplayTextProvider(state, mediaService);
        propertyNotifier = new MusicPropertyNotifier(propertyName => OnPropertyChanged(propertyName), displayTextProvider);
        // Set property change notifier so display provider can notify when language name loads asynchronously
        displayTextProvider.SetPropertyChangeNotifier(propertyName => OnPropertyChanged(propertyName));
        musicEnabledHandler = new MusicEnabledHandler(logger, dispatcher, serviceProvider, state);
        stateTracker = new MusicStateTracker();
        stateInitializer = new MusicStateInitializer(state, dispatcher, displayTextProvider, propertyNotifier);
        stateChangeHandler = new MusicStateChangeHandler(logger, state, dispatcher, mapper, serviceProvider, stateTracker, propertyNotifier, displayTextProvider);

        state.StateChanged += OnStateChanged;

        // Initialize scheduleId and isNewSchedule before creating commands
        // so commands capture the correct values
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            scheduleId = currentSchedule.Id;
            isNewSchedule = currentSchedule.Id <= 0;
        }

        InitializeCommands();
        InitializeFromState();
        _ = UpdateSelectabilityFlagsAsync();
    }

    private void InitializeFromState()
    {
        var (newScheduleId, newIsNewSchedule, initialMusicEnabled) = stateInitializer.InitializeFromState();
        scheduleId = newScheduleId;
        isNewSchedule = newIsNewSchedule;

        // Track initial MusicEnabled state when schedule page is first opened
        // This is used to determine if we should reset to default music on first enable
        if (!initialMusicEnabledOnPageLoad.HasValue && initialMusicEnabled.HasValue)
        {
            initialMusicEnabledOnPageLoad = initialMusicEnabled.Value;
        }

        // Initialize state tracker
        stateTracker.InitializeFromSchedule(state.Value.CurrentSchedule);
    }

    private void InitializeCommands()
    {
        SelectMusicCommand = commandInitializer.CreateSelectMusicCommand(
            () => stateHolder.Music, m => stateHolder.Music = m, scheduleId, isNewSchedule, stateHolder.MusicUpdated);
        SelectMusicTypeCommand = commandInitializer.CreateSelectMusicTypeCommand(
            () => stateHolder.Music, m => stateHolder.Music = m, scheduleId, isNewSchedule, stateHolder.MusicUpdated);
        SelectSongPublicationCommand = commandInitializer.CreateSelectSongPublicationCommand(
            () => stateHolder.Music, m => stateHolder.Music = m, scheduleId, isNewSchedule, stateHolder.MusicUpdated);
        SelectMusicSectionCommand = commandInitializer.CreateSelectMusicSectionCommand(
            () => stateHolder.Music, m => stateHolder.Music = m, scheduleId, isNewSchedule, stateHolder.MusicUpdated);
        SelectTrackCommand = commandInitializer.CreateSelectTrackCommand(
            () => stateHolder.Music, m => stateHolder.Music = m, scheduleId, isNewSchedule, stateHolder.MusicUpdated);
        SelectMusicLanguageCommand = commandInitializer.CreateSelectMusicLanguageCommand(
            () => stateHolder.Music, m => stateHolder.Music = m, scheduleId, isNewSchedule, stateHolder.MusicUpdated);
        ToggleRepeatCommand = commandInitializer.CreateToggleRepeatCommand();
        ToggleMusicEnabledCommand = new RelayCommand(() => MusicEnabled = !MusicEnabled);
    }

    public void SetMusicUpdated(bool musicUpdated)
    {
        stateHolder.MusicUpdated = musicUpdated;
    }

    public bool GetMusicUpdated()
    {
        return stateHolder.MusicUpdated;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Prevent re-entrant calls to avoid cycles
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;
            
            // Notify IsMusicSelectionVisible when schedule changes (category might have changed)
            OnPropertyChanged(nameof(IsMusicSelectionVisible));

            // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
            // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
            if (stateInitializer.ShouldReinitialize())
            {
                stateInitializer.ResetReadyFlags();
                // Re-initialize and signal ready again
                InitializeFromState();
                return;
            }

            // If we don't have a scheduleId yet (initial state), initialize when CurrentSchedule is set
            // But only if we haven't already signaled ready (prevents infinite loop for new schedules with Id=0)
            if (scheduleId == 0 && currentSchedule != null && !stateInitializer.HasSignaledReady)
            {
                // Reset initial MusicEnabled tracking when a new schedule is opened
                initialMusicEnabledOnPageLoad = null;
                InitializeFromState();
                // Recreate commands with updated scheduleId and isNewSchedule
                InitializeCommands();
                return;
            }

            // Initialize if schedule ID changed (new schedule opened)
            if (currentSchedule != null && currentSchedule.Id != scheduleId)
            {
                // Reset initial MusicEnabled tracking when a new schedule is opened
                initialMusicEnabledOnPageLoad = null;
                stateInitializer.ResetReadyFlags();
                InitializeFromState();
                // Recreate commands with updated scheduleId and isNewSchedule
                InitializeCommands();
            }

            // Handle state changes using helper
            stateChangeHandler.HandleStateChanged(
                scheduleId,
                stateHolder,
                initialMusicEnabledOnPageLoad,
                (val) => ShouldScrollToBottom = val,
                (propertyName) => OnPropertyChanged(propertyName));

            // Update selectability flags when state changes
            _ = UpdateSelectabilityFlagsAsync();
        }
        finally
        {
            isProcessingStateChange = false;
        }
    }

    private async Task UpdateSelectabilityFlagsAsync()
    {
        try
        {
            // During save/cancel/delete, the schedule page overlay is shown and the user cannot interact.
            // Avoid triggering expensive DB queries for "is selectable?" checks while the page is busy.
            if (state.Value.IsSchedulePageOverlayVisible)
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            // Only recompute when music-related inputs change (prevents DB calls on unrelated state updates,
            // e.g., changing the Bible publication category).
            var signature = string.Join('|',
                currentSchedule.MusicEnabled.ToString(),
                currentSchedule.MusicLanguageCode ?? string.Empty,
                currentSchedule.MusicPublicationCode ?? string.Empty,
                currentSchedule.MusicSectionCode ?? string.Empty,
                currentSchedule.MusicTrackCode?.ToString() ?? string.Empty,
                currentSchedule.MusicPublicationModalItemCount?.ToString() ?? string.Empty,
                currentSchedule.MusicSectionModalItemCount?.ToString() ?? string.Empty);

            if (string.Equals(signature, lastSelectabilitySignature, StringComparison.Ordinal))
            {
                return;
            }

            lastSelectabilitySignature = signature;

            // Update flags asynchronously without blocking UI
            var languageSelectable = await displayTextProvider.GetIsMusicLanguageSelectableAsync();
            var publicationSelectable = await displayTextProvider.GetIsSongPublicationSelectableAsync();
            var sectionSelectable = await displayTextProvider.GetIsMusicSectionSelectableAsync();
            var trackSelectable = await displayTextProvider.GetIsMusicTrackSelectableAsync();

            // Update on main thread to trigger property change notifications
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isMusicLanguageSelectable != languageSelectable)
                {
                    isMusicLanguageSelectable = languageSelectable;
                    OnPropertyChanged(nameof(IsMusicLanguageSelectable));
                }

                if (isSongPublicationSelectable != publicationSelectable)
                {
                    isSongPublicationSelectable = publicationSelectable;
                    OnPropertyChanged(nameof(IsSongPublicationSelectable));
                }

                if (isMusicSectionSelectable != sectionSelectable)
                {
                    isMusicSectionSelectable = sectionSelectable;
                    OnPropertyChanged(nameof(IsMusicSectionSelectable));
                }

                if (isMusicTrackSelectable != trackSelectable)
                {
                    isMusicTrackSelectable = trackSelectable;
                    OnPropertyChanged(nameof(IsMusicTrackSelectable));
                }
            });
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "MusicSelectionContainerViewModel: Error updating selectability flags");
        }
    }

    public ICommand SelectMusicCommand { get; private set; } = null!;
    public ICommand SelectMusicTypeCommand { get; private set; } = null!;
    public ICommand SelectMusicLanguageCommand { get; private set; } = null!;
    public ICommand SelectSongPublicationCommand { get; private set; } = null!;
    public ICommand SelectMusicSectionCommand { get; private set; } = null!;
    public ICommand SelectTrackCommand { get; private set; } = null!;
    public ICommand ToggleRepeatCommand { get; private set; } = null!;
    public ICommand ToggleMusicEnabledCommand { get; private set; } = null!;

    public bool MusicEnabled
    {
        get
        {
            // Return pending value if set (optimistic update), otherwise read from state
            if (stateHolder.PendingMusicEnabled.HasValue)
            {
                return stateHolder.PendingMusicEnabled.Value;
            }
            var currentSchedule = state.Value.CurrentSchedule;
            return currentSchedule?.MusicEnabled ?? false;
        }
        set
        {
            var currentValue = MusicEnabled;
            musicEnabledHandler.HandleSetMusicEnabled(
                value,
                currentValue,
                stateHolder.IsUpdatingFromState,
                initialMusicEnabledOnPageLoad,
                (val) => stateHolder.PendingMusicEnabled = val,
                (val) => ShouldScrollToBottom = val,
                () => OnPropertyChanged(nameof(MusicEnabled)));
        }
    }

    public bool IsSongPublicationVisible => displayTextProvider.GetIsSongPublicationVisible();
    public bool IsMusicLanguageVisible => displayTextProvider.GetIsMusicLanguageVisible();
    public string MusicLanguageDisplayText => displayTextProvider.GetMusicLanguageDisplayText();
    public string SongPublicationDisplayText => displayTextProvider.GetSongPublicationDisplayText();
    public async Task<string> GetSongPublicationDisplayTextAsync() => await displayTextProvider.GetSongPublicationDisplayTextAsync();
    public bool IsMusicSectionVisible => displayTextProvider.GetIsMusicSectionVisible();
    public string MusicSectionDisplayText => displayTextProvider.GetMusicSectionDisplayText();
    public string TrackDisplayText => displayTextProvider.GetTrackDisplayText();
    public async Task<string> GetTrackDisplayTextAsync() => await displayTextProvider.GetTrackDisplayTextAsync();
    public bool IsRepeatEnabled => displayTextProvider.GetIsRepeatEnabled();
    public bool HasTrackSelected => displayTextProvider.GetHasTrackSelected();

    /// <summary>
    /// Gets the FlowDirection for the music container based on the music's selected language direction.
    /// Used for song publication and track rows which display RTL content (e.g., Arabic song titles).
    /// </summary>
    public FlowDirection ContentFlowDirection => displayTextProvider.GetFlowDirection();

    // Selectability properties - rows are only tappable if there are multiple options
    public bool IsMusicLanguageSelectable => isMusicLanguageSelectable;
    public bool IsSongPublicationSelectable => isSongPublicationSelectable;
    public bool IsMusicSectionSelectable => isMusicSectionSelectable;
    public bool IsMusicTrackSelectable => isMusicTrackSelectable;

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

