#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class BiblePublicationSelectionContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    // Helper classes for modular functionality
    private readonly BiblePublicationCommandInitializer commandInitializer;
    private readonly BiblePublicationDisplayTextProvider displayTextProvider;
    private readonly BiblePublicationPropertyChangeDetector propertyChangeDetector;
    private readonly BiblePublicationPropertyNotifier propertyNotifier;

    private int scheduleId;
    private bool isNewSchedule;
    private bool isProcessingStateChange;
    private bool biblePublicationUpdated;
    private BiblePublicationSchedule? biblePublicationSchedule;
    private bool hasSignaledReady;
    private bool isReadyActionQueued;

    // Track the last processed state to prevent processing the same state multiple times
    private int? lastProcessedScheduleId;
    private string? lastProcessedLanguageCode;
    private string? lastProcessedLanguageName;
    private string? lastProcessedPublicationCode;
    private string? lastProcessedPublicationName;
    private string? lastProcessedSectionCode;
    private string? lastProcessedSectionName;
    private string? lastProcessedTrackCode;
    private bool shouldScrollToContainer;

    // Cached selectability flags (updated when state changes)
    private bool isCategorySelectable = true; // Categories are typically always selectable (3 categories)
    private bool isLanguageSelectable = false;
    private bool isPublicationSelectable = false;
    private bool isSectionSelectable = false;
    private bool isTrackSelectable = false;
    private string? lastSelectabilitySignature;

    public BiblePublicationSelectionContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Initialize helper classes
        commandInitializer = new BiblePublicationCommandInitializer(logger, navigationService, scheduleSelectionService, state, dispatcher, mapper, serviceProvider);
        var mediaService = serviceProvider.GetRequiredService<Bible.Alarm.Services.Media.Interfaces.IMediaService>();
        var categoryNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ICategoryNameService>();
        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        displayTextProvider = new BiblePublicationDisplayTextProvider(state, logger, mediaService, categoryNameService, serviceScopeFactory);
        propertyChangeDetector = new BiblePublicationPropertyChangeDetector(displayTextProvider);
        propertyNotifier = new BiblePublicationPropertyNotifier(propertyName => OnPropertyChanged(propertyName));

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

            // Initialize last values from CurrentSchedule (single source of truth)
            propertyChangeDetector.Initialize(
                currentSchedule.BiblePublicationCategoryId,
                currentSchedule.BiblePublicationCategoryName,
                currentSchedule.BiblePublicationLanguageCode,
                currentSchedule.BiblePublicationCode,
                currentSchedule.BiblePublicationSectionCode,
                currentSchedule.BiblePublicationTrackCode);

            // Initialize last processed state to prevent duplicate processing
            lastProcessedScheduleId = currentSchedule.Id;
            lastProcessedLanguageCode = currentSchedule.BiblePublicationLanguageCode;

            // Update selectability flags on initial load
            _ = UpdateSelectabilityFlagsAsync();
            lastProcessedLanguageName = currentSchedule.BiblePublicationLanguageName;
            lastProcessedPublicationCode = currentSchedule.BiblePublicationCode;
            lastProcessedPublicationName = currentSchedule.BiblePublicationName;
            lastProcessedSectionCode = currentSchedule.BiblePublicationSectionCode;
            lastProcessedSectionName = currentSchedule.BiblePublicationSectionName;
            lastProcessedTrackCode = currentSchedule.BiblePublicationTrackCode;

            // Batch property notifications to reduce UI thread work
            propertyNotifier.NotifyAllDisplayTextPropertiesChanged();

            // Signal that this container is ready (initialized from CurrentSchedule)
            SignalContainerReady();
        }
    }

    private void SignalContainerReady()
    {
        // Check if already signaled or already marked ready in state
        // This check must happen first to prevent any duplicate work
        if (hasSignaledReady || state.Value.ContainerReadiness.BiblePublicationSelection) return;

        // Check if action is already queued to prevent duplicate queued actions
        // This prevents multiple rapid calls from queuing multiple actions
        if (isReadyActionQueued) return;

        // Atomically set both flags to prevent race conditions
        // If another thread/call checks between these lines, it will see isReadyActionQueued=true
        isReadyActionQueued = true;
        hasSignaledReady = true;

        // Double-check state immediately after setting flags (before queuing)
        // This catches the case where state changed between the initial check and flag setting
        if (state.Value.ContainerReadiness.BiblePublicationSelection)
        {
            // State already shows ready, reset flags and return
            isReadyActionQueued = false;
            hasSignaledReady = true;
            return;
        }

        // Dispatch to state that this container is ready
        // Check state again inside the queued action to prevent duplicates from queued actions
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Reset flag when action executes
            isReadyActionQueued = false;

            // Final check before dispatching - if state already shows we're ready, another action already handled it
            if (state.Value.ContainerReadiness.BiblePublicationSelection)
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("BiblePublicationSelection"));
        });
    }

    private void InitializeCommands()
    {
        SelectCategoryCommand = commandInitializer.CreateSelectCategoryCommand();
        SelectLanguageCommand = commandInitializer.CreateSelectLanguageCommand();
        SelectBibleCommand = commandInitializer.CreateSelectBibleCommand(
            () => biblePublicationSchedule, b => biblePublicationSchedule = b, scheduleId, isNewSchedule, biblePublicationUpdated);
        SelectSectionCommand = commandInitializer.CreateSelectSectionCommand(
            () => biblePublicationSchedule, b => biblePublicationSchedule = b, scheduleId, isNewSchedule, biblePublicationUpdated);
        SelectTrackCommand = commandInitializer.CreateSelectTrackCommand(
            () => biblePublicationSchedule, b => biblePublicationSchedule = b, scheduleId, isNewSchedule, biblePublicationUpdated);
    }

    public void SetScheduleId(int scheduleId, bool isNewSchedule)
    {
        this.scheduleId = scheduleId;
        this.isNewSchedule = isNewSchedule;
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
            // CurrentSchedule is the single source of truth - no need for separate currentBiblePublication

            // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
            // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
            if (hasSignaledReady && !stateValue.ContainerReadiness.BiblePublicationSelection && currentSchedule != null)
            {
                hasSignaledReady = false;
                isReadyActionQueued = false; // Reset queued flag as well
                // Re-initialize and signal ready again
                InitializeFromState();
                return;
            }

            if (!ShouldProcessStateChange(currentSchedule, out var isInitialLoad))
            {
                return;
            }

            // For initial load (scheduleId was 0), call InitializeFromState to set up and signal ready
            if (isInitialLoad)
            {
                InitializeFromState();
                _ = UpdateSelectabilityFlagsAsync();
                return;
            }

            // Update selectability flags when state changes
            _ = UpdateSelectabilityFlagsAsync();

            // Early exit if we've already processed this exact state
            // Also check display names (language name, publication name, section name) to ensure display text updates when they change
            if (currentSchedule != null &&
                currentSchedule.Id == lastProcessedScheduleId &&
                currentSchedule.BiblePublicationLanguageCode == lastProcessedLanguageCode &&
                currentSchedule.BiblePublicationLanguageName == lastProcessedLanguageName &&
                currentSchedule.BiblePublicationCode == lastProcessedPublicationCode &&
                currentSchedule.BiblePublicationName == lastProcessedPublicationName &&
                string.Equals(currentSchedule.BiblePublicationSectionCode, lastProcessedSectionCode, StringComparison.OrdinalIgnoreCase) &&
                currentSchedule.BiblePublicationSectionName == lastProcessedSectionName &&
                currentSchedule.BiblePublicationTrackCode == lastProcessedTrackCode)
            {
                return;
            }

            LogStateChange(currentSchedule);
            HandleScheduleIdChange(currentSchedule);
            UpdateBiblePublicationUpdatedFlag(currentSchedule);

            var changeInfo = propertyChangeDetector.DetectPropertyChanges(currentSchedule);
            if (changeInfo.HasChanges)
            {
                // IMPORTANT:
                // Do NOT reset BiblePublicationFinishedDuration here.
                // Progress reset is applied only on Save, after comparing against the initially-opened track.

                // Set scroll flag if section or track changed (user made a selection)
                var shouldScroll = changeInfo.NotifySection || changeInfo.NotifyTrack;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    propertyNotifier.NotifyPropertyChanges(changeInfo);
                    if (shouldScroll)
                    {
                        ShouldScrollToContainer = true;
                    }
                });
            }

            // Update last processed state after handling changes
            if (currentSchedule != null)
            {
                lastProcessedScheduleId = currentSchedule.Id;
                lastProcessedLanguageCode = currentSchedule.BiblePublicationLanguageCode;
                lastProcessedLanguageName = currentSchedule.BiblePublicationLanguageName;
                lastProcessedPublicationCode = currentSchedule.BiblePublicationCode;
                lastProcessedPublicationName = currentSchedule.BiblePublicationName;
            lastProcessedSectionCode = currentSchedule.BiblePublicationSectionCode;
                lastProcessedSectionName = currentSchedule.BiblePublicationSectionName;
                lastProcessedTrackCode = currentSchedule.BiblePublicationTrackCode;
            }
        }
        finally
        {
            isProcessingStateChange = false;
        }
    }

    private bool ShouldProcessStateChange(ScheduleStateItem? currentSchedule, out bool isInitialLoad)
    {
        isInitialLoad = false;

        // If we don't have a scheduleId yet (initial state), always process when CurrentSchedule is set
        // But only if we haven't already signaled ready (prevents infinite loop for new schedules with Id=0)
        if (scheduleId == 0 && currentSchedule != null && !hasSignaledReady)
        {
            isInitialLoad = true;
            return true;
        }

        // If CurrentSchedule is null, skip (unless we're waiting for it to be set)
        if (currentSchedule == null)
        {
            return false;
        }

        // If schedule IDs don't match and we already have a scheduleId, skip
        if (scheduleId > 0 && currentSchedule.Id > 0 && currentSchedule.Id != scheduleId)
        {
            return false;
        }

        return true;
    }

    private void LogStateChange(ScheduleStateItem? currentSchedule)
    {
        // Logging removed - not needed for normal operation
    }

    private void HandleScheduleIdChange(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
        {
            // Reset for new schedule
            hasSignaledReady = false;
            // Reset queued flag as well
            isReadyActionQueued = false;
            InitializeFromState();
            // Reset last processed state to ensure new schedule is processed
            lastProcessedScheduleId = null;
        }
    }

    private void UpdateBiblePublicationUpdatedFlag(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null)
        {
            var hasBiblePublication = currentSchedule.BiblePublicationScheduleId.HasValue;
            if (hasBiblePublication && currentSchedule.BiblePublicationScheduleId.HasValue &&
                (biblePublicationSchedule == null ||
                biblePublicationSchedule.Id != currentSchedule.BiblePublicationScheduleId.Value))
            {
                biblePublicationUpdated = true;
            }
        }
    }

    // NOTE: Progress is intentionally NOT reset in this container.
    // It is reset only on Save, based on comparing the saved track to the originally-opened track.


    public ICommand SelectCategoryCommand { get; private set; } = null!;
    public ICommand SelectLanguageCommand { get; private set; } = null!;
    public ICommand SelectBibleCommand { get; private set; } = null!;
    public ICommand SelectSectionCommand { get; private set; } = null!;
    public ICommand SelectTrackCommand { get; private set; } = null!;

    public bool IsSectionVisible => displayTextProvider.GetIsSectionVisible();
    public bool IsLanguageVisible => BiblePublicationDisplayTextProvider.GetIsLanguageVisible();
    public FlowDirection ContentFlowDirection => displayTextProvider.GetFlowDirection();
    public string CategoryDisplayText => displayTextProvider.GetCategoryDisplayText();
    public string LanguageDisplayText => displayTextProvider.GetLanguageDisplayText();
    public string PublicationDisplayText => displayTextProvider.GetPublicationDisplayText();
    public string SectionDisplayText => displayTextProvider.GetSectionDisplayText();
    public string TrackDisplayText => displayTextProvider.GetTrackDisplayText();

    // Selectability properties - rows are only tappable if there are multiple options
    public bool IsCategorySelectable => isCategorySelectable;
    public bool IsLanguageSelectable => isLanguageSelectable;
    public bool IsPublicationSelectable => isPublicationSelectable;
    public bool IsSectionSelectable => isSectionSelectable;
    public bool IsTrackSelectable => isTrackSelectable;

    public bool ShouldScrollToContainer
    {
        get => shouldScrollToContainer;
        set => SetProperty(ref shouldScrollToContainer, value);
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

            // Only recompute when relevant inputs change (prevents DB calls on unrelated state updates).
            // Modal counts are not included because selectability now queries the discovery DB directly
            // with its own caching, making it resilient to stale Fluxor state.
            var signature = string.Join('|',
                currentSchedule.BiblePublicationCategoryName ?? string.Empty,
                currentSchedule.BiblePublicationLanguageCode ?? string.Empty,
                currentSchedule.BiblePublicationCode ?? string.Empty,
                currentSchedule.BiblePublicationSectionCode ?? string.Empty);

            if (string.Equals(signature, lastSelectabilitySignature, StringComparison.Ordinal))
            {
                return;
            }

            lastSelectabilitySignature = signature;

            // Update flags asynchronously without blocking UI
            var languageSelectable = await displayTextProvider.GetIsLanguageSelectableAsync();
            var publicationSelectable = await displayTextProvider.GetIsPublicationSelectableAsync();
            var sectionSelectable = await displayTextProvider.GetIsSectionSelectableAsync();
            var trackSelectable = await displayTextProvider.GetIsTrackSelectableAsync();

            // Update on main thread to trigger property change notifications
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isLanguageSelectable != languageSelectable)
                {
                    isLanguageSelectable = languageSelectable;
                    OnPropertyChanged(nameof(IsLanguageSelectable));
                }

                if (isPublicationSelectable != publicationSelectable)
                {
                    isPublicationSelectable = publicationSelectable;
                    OnPropertyChanged(nameof(IsPublicationSelectable));
                }

                if (isSectionSelectable != sectionSelectable)
                {
                    isSectionSelectable = sectionSelectable;
                    OnPropertyChanged(nameof(IsSectionSelectable));
                }

                if (isTrackSelectable != trackSelectable)
                {
                    isTrackSelectable = trackSelectable;
                    OnPropertyChanged(nameof(IsTrackSelectable));
                }
            });
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "BiblePublicationSelectionContainerViewModel: Error updating selectability flags");
        }
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

