#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class BibleSelectionContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    // Helper classes for modular functionality
    private readonly BibleCommandInitializer commandInitializer;
    private readonly BibleDisplayTextProvider displayTextProvider;
    private readonly BiblePropertyChangeDetector propertyChangeDetector;
    private readonly BiblePropertyNotifier propertyNotifier;

    private int scheduleId;
    private bool isNewSchedule;
    private bool bibleReadingUpdated;
    private BibleReadingSchedule? bibleReadingSchedule;
    private bool hasSignaledReady;
    private bool isReadyActionQueued;

    // Track if we've already reset progress for the current cascade change to prevent loops
    private bool progressResetForCurrentCascade;

    // Track the last processed state to prevent processing the same state multiple times
    private int? lastProcessedScheduleId;
    private string? lastProcessedLanguageCode;
    private string? lastProcessedLanguageName;
    private string? lastProcessedPublicationCode;
    private string? lastProcessedPublicationName;
    private int? lastProcessedBookNumber;
    private string? lastProcessedBookName;
    private int? lastProcessedChapterNumber;

    public BibleSelectionContainerViewModel(
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
        commandInitializer = new BibleCommandInitializer(logger, navigationService, scheduleSelectionService, state, dispatcher, mapper, serviceProvider);
        displayTextProvider = new BibleDisplayTextProvider(state, logger);
        propertyChangeDetector = new BiblePropertyChangeDetector(displayTextProvider);
        propertyNotifier = new BiblePropertyNotifier(propertyName => OnPropertyChanged(propertyName));

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

            // Initialize last values
            var currentBibleReading = state.Value.CurrentBibleReadingSchedule;
            propertyChangeDetector.Initialize(
                currentSchedule.BibleReadingLanguageCode,
                currentBibleReading?.PublicationCode ?? currentSchedule.BibleReadingPublicationCode,
                currentBibleReading?.BookNumber ?? currentSchedule.BibleReadingBookNumber,
                currentBibleReading?.ChapterNumber ?? currentSchedule.BibleReadingChapterNumber);

            // Initialize last processed state to prevent duplicate processing
            lastProcessedScheduleId = currentSchedule.Id;
            lastProcessedLanguageCode = currentSchedule.BibleReadingLanguageCode;
            lastProcessedLanguageName = currentSchedule.BibleReadingLanguageName;
            lastProcessedPublicationCode = currentBibleReading?.PublicationCode ?? currentSchedule.BibleReadingPublicationCode;
            lastProcessedPublicationName = currentSchedule.BibleReadingPublicationName;
            lastProcessedBookNumber = currentBibleReading?.BookNumber ?? currentSchedule.BibleReadingBookNumber;
            lastProcessedBookName = currentSchedule.BibleReadingBookName;
            lastProcessedChapterNumber = currentBibleReading?.ChapterNumber ?? currentSchedule.BibleReadingChapterNumber;

            // Batch property notifications to reduce UI thread work
            propertyNotifier.NotifyAllDisplayTextPropertiesChanged();
            
            // Signal that this container is ready (initialized from CurrentSchedule)
            SignalContainerReady();
        }
    }

    private void SignalContainerReady()
    {
        // Check if already signaled or already marked ready in state
        if (hasSignaledReady || state.Value.ContainerReadiness.BibleSelection) return;
        
        // Check if action is already queued to prevent duplicate queued actions
        if (isReadyActionQueued) return;
        isReadyActionQueued = true;
        hasSignaledReady = true;
        
        // Dispatch to state that this container is ready
        // Check state again inside the queued action to prevent race conditions
        MainThread.BeginInvokeOnMainThread(() =>
        {
            isReadyActionQueued = false; // Reset flag when action executes
            
            // Double-check state before dispatching to prevent duplicates from queued actions
            // If state already shows we're ready, another action already handled it
            if (state.Value.ContainerReadiness.BibleSelection)
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("BibleSelection"));
        });
    }

    private void InitializeCommands()
    {
        SelectLanguageCommand = commandInitializer.CreateSelectLanguageCommand();
        SelectBibleCommand = commandInitializer.CreateSelectBibleCommand(
            () => bibleReadingSchedule, b => bibleReadingSchedule = b, scheduleId, isNewSchedule, bibleReadingUpdated);
        SelectBookCommand = commandInitializer.CreateSelectBookCommand(
            () => bibleReadingSchedule, b => bibleReadingSchedule = b, scheduleId, isNewSchedule, bibleReadingUpdated);
        SelectChapterCommand = commandInitializer.CreateSelectChapterCommand(
            () => bibleReadingSchedule, b => bibleReadingSchedule = b, scheduleId, isNewSchedule, bibleReadingUpdated);
    }

    public void SetScheduleId(int scheduleId, bool isNewSchedule)
    {
        this.scheduleId = scheduleId;
        this.isNewSchedule = isNewSchedule;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;
        var currentBibleReading = stateValue.CurrentBibleReadingSchedule;

        // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
        // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
        if (hasSignaledReady && !stateValue.ContainerReadiness.BibleSelection && currentSchedule != null)
        {
            hasSignaledReady = false;
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
            return;
        }

        // Early exit if we've already processed this exact state
        // Also check display names (language name, publication name, book name) to ensure display text updates when they change
        if (currentSchedule != null &&
            currentSchedule.Id == lastProcessedScheduleId &&
            currentSchedule.BibleReadingLanguageCode == lastProcessedLanguageCode &&
            currentSchedule.BibleReadingLanguageName == lastProcessedLanguageName &&
            (currentBibleReading?.PublicationCode ?? currentSchedule.BibleReadingPublicationCode) == lastProcessedPublicationCode &&
            currentSchedule.BibleReadingPublicationName == lastProcessedPublicationName &&
            (currentBibleReading?.BookNumber ?? currentSchedule.BibleReadingBookNumber) == lastProcessedBookNumber &&
            currentSchedule.BibleReadingBookName == lastProcessedBookName &&
            (currentBibleReading?.ChapterNumber ?? currentSchedule.BibleReadingChapterNumber) == lastProcessedChapterNumber)
        {
            return;
        }

        LogStateChange(currentSchedule, currentBibleReading);
        HandleScheduleIdChange(currentSchedule);
        UpdateBibleReadingUpdatedFlag(currentSchedule);

        var changeInfo = propertyChangeDetector.DetectPropertyChanges(currentSchedule, currentBibleReading);
        if (changeInfo.HasChanges)
        {
            // Reset the flag when a new cascade change is detected (before updating last values)
            if (changeInfo.CascadeChangeOccurred)
            {
                progressResetForCurrentCascade = false;
            }

            ResetProgressIfNeeded(currentSchedule, changeInfo);
            MainThread.BeginInvokeOnMainThread(() => propertyNotifier.NotifyPropertyChanges(changeInfo));
        }

        // Update last processed state after handling changes
        if (currentSchedule != null)
        {
            lastProcessedScheduleId = currentSchedule.Id;
            lastProcessedLanguageCode = currentSchedule.BibleReadingLanguageCode;
            lastProcessedLanguageName = currentSchedule.BibleReadingLanguageName;
            lastProcessedPublicationCode = currentBibleReading?.PublicationCode ?? currentSchedule.BibleReadingPublicationCode;
            lastProcessedPublicationName = currentSchedule.BibleReadingPublicationName;
            lastProcessedBookNumber = currentBibleReading?.BookNumber ?? currentSchedule.BibleReadingBookNumber;
            lastProcessedBookName = currentSchedule.BibleReadingBookName;
            lastProcessedChapterNumber = currentBibleReading?.ChapterNumber ?? currentSchedule.BibleReadingChapterNumber;
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

    private void LogStateChange(ScheduleStateItem? currentSchedule, BibleReadingStateItem? currentBibleReading)
    {
        // Logging removed - not needed for normal operation
    }

    private void HandleScheduleIdChange(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
        {
            hasSignaledReady = false; // Reset for new schedule
            InitializeFromState();
            // Reset last processed state to ensure new schedule is processed
            lastProcessedScheduleId = null;
        }
    }

    private void UpdateBibleReadingUpdatedFlag(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null)
        {
            var hasBibleReading = currentSchedule.BibleReadingScheduleId.HasValue;
            if (hasBibleReading && currentSchedule.BibleReadingScheduleId.HasValue &&
                (bibleReadingSchedule == null ||
                bibleReadingSchedule.Id != currentSchedule.BibleReadingScheduleId.Value))
            {
                bibleReadingUpdated = true;
            }
        }
    }

    private void ResetProgressIfNeeded(ScheduleStateItem? currentSchedule, BiblePropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        if (changeInfo.CascadeChangeOccurred && currentSchedule != null)
        {
            var currentProgress = currentSchedule.BibleReadingFinishedDuration ?? TimeSpan.Zero;
            // Only reset if progress is non-zero AND we haven't already reset for this cascade change
            if (currentProgress != TimeSpan.Zero && !progressResetForCurrentCascade)
            {
                progressResetForCurrentCascade = true;
                // Run DeepClone and mapping on background thread to avoid blocking UI
                _ = Task.Run(() =>
                {
                    var clonedSchedule = currentSchedule.DeepClone();
                    var scheduleStateItem = mapper.Map<ScheduleStateItem>(clonedSchedule);
                    scheduleStateItem.BibleReadingFinishedDuration = TimeSpan.Zero;
                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, true, shouldSave: false));
                });
            }
        }
    }


    public ICommand SelectLanguageCommand { get; private set; } = null!;
    public ICommand SelectBibleCommand { get; private set; } = null!;
    public ICommand SelectBookCommand { get; private set; } = null!;
    public ICommand SelectChapterCommand { get; private set; } = null!;

    public string LanguageDisplayText => displayTextProvider.GetLanguageDisplayText();
    public string TranslationDisplayText => displayTextProvider.GetTranslationDisplayText();
    public string BookDisplayText => displayTextProvider.GetBookDisplayText();
    public string ChapterDisplayText => displayTextProvider.GetChapterDisplayText();

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

