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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class BibleSelectionContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IScheduleSelectionService scheduleSelectionService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    private int scheduleId;
    private bool isNewSchedule;
    private bool bibleReadingUpdated;
    private BibleReadingSchedule? bibleReadingSchedule;
    private AlarmSchedule? model;

    // Track last values to prevent unnecessary PropertyChanged notifications
    private string? lastLanguageDisplayText;
    private string? lastTranslationDisplayText;
    private string? lastBookDisplayText;
    private string? lastChapterDisplayText;

    // Track underlying property values to detect cascading changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private int? lastBookNumber;
    private int? lastChapterNumber;

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
        this.navigationService = navigationService;
        this.scheduleSelectionService = scheduleSelectionService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;

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
            lastLanguageCode = currentSchedule.BibleReadingLanguageCode;
            lastPublicationCode = currentBibleReading?.PublicationCode ?? currentSchedule.BibleReadingPublicationCode;
            lastBookNumber = currentBibleReading?.BookNumber ?? currentSchedule.BibleReadingBookNumber;
            lastChapterNumber = currentBibleReading?.ChapterNumber ?? currentSchedule.BibleReadingChapterNumber;
            lastLanguageDisplayText = LanguageDisplayText;
            lastTranslationDisplayText = TranslationDisplayText;
            lastBookDisplayText = BookDisplayText;
            lastChapterDisplayText = ChapterDisplayText;

            OnPropertyChanged(nameof(LanguageDisplayText));
            OnPropertyChanged(nameof(TranslationDisplayText));
            OnPropertyChanged(nameof(BookDisplayText));
            OnPropertyChanged(nameof(ChapterDisplayText));
        }
    }

    private void InitializeCommands()
    {
        SelectLanguageCommand = new AsyncRelayCommand(async () =>
        {
            logger.Information("BibleSelectionContainerViewModel: SelectLanguageCommand - Opening language modal");
            // Create a temporary BibleSelectionViewModel instance for the language modal
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BibleSelectionViewModel>();
            logger.Debug("BibleSelectionContainerViewModel: SelectLanguageCommand - Created BibleSelectionViewModel, opening modal");
            await navigationService.OpenLanguageModalAsync(bibleSelectionViewModel);
            logger.Debug("BibleSelectionContainerViewModel: SelectLanguageCommand - Modal opened");
        });

        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            bibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, bibleReadingSchedule));

            // Create view model and open modal
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BibleSelectionViewModel>();
            await navigationService.OpenBibleSelectionModalAsync(bibleSelectionViewModel);

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = bibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(bibleReadingSchedule)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BibleSelectionAction(currentBibleReadingItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });

        SelectBookCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            bibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, bibleReadingSchedule));

            // Create view model and open modal
            var bookSelectionViewModel = serviceProvider.GetRequiredService<BookSelectionViewModel>();
            await navigationService.OpenBookSelectionModalAsync(bookSelectionViewModel);

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = bibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(bibleReadingSchedule)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BookSelectionAction(currentBibleReadingItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });

        SelectChapterCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            bibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, bibleReadingSchedule));

            // Create view model and open modal
            var chapterSelectionViewModel = serviceProvider.GetRequiredService<ChapterSelectionViewModel>();
            await navigationService.OpenChapterSelectionModalAsync(chapterSelectionViewModel);

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = bibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(bibleReadingSchedule)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new ChapterSelectionAction(currentBibleReadingItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });

    }

    public void SetModel(AlarmSchedule model)
    {
        this.model = model;
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

        if (!ShouldProcessStateChange(currentSchedule))
        {
            return;
        }

        LogStateChange(currentSchedule, currentBibleReading);
        HandleScheduleIdChange(currentSchedule);
        UpdateBibleReadingUpdatedFlag(currentSchedule);

        var changeInfo = DetectPropertyChanges(currentSchedule, currentBibleReading);
        if (changeInfo.HasChanges)
        {
            UpdateLastValues(changeInfo);
            ResetProgressIfNeeded(currentSchedule, changeInfo);
            NotifyPropertyChanges(changeInfo);
        }
    }

    private bool ShouldProcessStateChange(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null || (scheduleId > 0 && currentSchedule.Id > 0 && currentSchedule.Id != scheduleId))
        {
            logger.Debug("BibleSelectionContainerViewModel: OnStateChanged - Skipping state change. CurrentSchedule: {CurrentSchedule}, OurScheduleId: {ScheduleId}",
                currentSchedule != null ? $"Id={currentSchedule.Id}" : "null", scheduleId);
            return false;
        }
        return true;
    }

    private void LogStateChange(ScheduleStateItem? currentSchedule, BibleReadingStateItem? currentBibleReading)
    {
        logger.Information("BibleSelectionContainerViewModel: OnStateChanged - CurrentSchedule: {CurrentSchedule}, CurrentBibleReadingSchedule: {CurrentBibleReadingSchedule}",
            currentSchedule != null ? $"Id={currentSchedule.Id}" : "null",
            currentBibleReading != null ? $"LanguageCode={currentBibleReading.LanguageCode}, PublicationCode={currentBibleReading.PublicationCode}" : "null");

        if (currentSchedule != null)
        {
            logger.Debug("BibleSelectionContainerViewModel: OnStateChanged - CurrentSchedule details. Id: {ScheduleId}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}, BookNumber: {BookNumber}, BookName: {BookName}, ChapterNumber: {ChapterNumber}",
                currentSchedule.Id,
                currentSchedule.BibleReadingLanguageCode ?? "null",
                currentSchedule.BibleReadingLanguageName ?? "null",
                currentSchedule.BibleReadingPublicationCode ?? "null",
                currentSchedule.BibleReadingPublicationName ?? "null",
                currentSchedule.BibleReadingBookNumber ?? 0,
                currentSchedule.BibleReadingBookName ?? "null",
                currentSchedule.BibleReadingChapterNumber ?? 0);
        }
    }

    private void HandleScheduleIdChange(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null && currentSchedule.Id != scheduleId)
        {
            logger.Debug("BibleSelectionContainerViewModel: OnStateChanged - Schedule ID changed from {OldScheduleId} to {NewScheduleId}, initializing from state",
                scheduleId, currentSchedule.Id);
            InitializeFromState();
        }
    }

    private void UpdateBibleReadingUpdatedFlag(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null && model != null)
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

    private PropertyChangeInfo DetectPropertyChanges(ScheduleStateItem? currentSchedule, BibleReadingStateItem? currentBibleReading)
    {
        var currentLanguageCode = currentSchedule?.BibleReadingLanguageCode;
        var currentPublicationCode = currentBibleReading?.PublicationCode ?? currentSchedule?.BibleReadingPublicationCode;
        var currentBookNumber = currentBibleReading?.BookNumber ?? currentSchedule?.BibleReadingBookNumber;
        var currentChapterNumber = currentBibleReading?.ChapterNumber ?? currentSchedule?.BibleReadingChapterNumber;

        var languageCodeChanged = currentLanguageCode != lastLanguageCode;
        var publicationCodeChanged = currentPublicationCode != lastPublicationCode;
        var bookNumberChanged = currentBookNumber != lastBookNumber;
        var chapterNumberChanged = currentChapterNumber != lastChapterNumber;

        var newLanguageDisplayText = LanguageDisplayText;
        var newTranslationDisplayText = TranslationDisplayText;
        var newBookDisplayText = BookDisplayText;
        var newChapterDisplayText = ChapterDisplayText;

        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var translationDisplayChanged = newTranslationDisplayText != lastTranslationDisplayText;
        var bookDisplayChanged = newBookDisplayText != lastBookDisplayText;
        var chapterDisplayChanged = newChapterDisplayText != lastChapterDisplayText;

        var notifyLanguage = languageCodeChanged;
        var notifyTranslation = languageCodeChanged || publicationCodeChanged;
        var notifyBook = languageCodeChanged || publicationCodeChanged || bookNumberChanged;
        var notifyChapter = languageCodeChanged || publicationCodeChanged || bookNumberChanged || chapterNumberChanged;

        var displayTextOnlyChanged = (languageDisplayChanged && !languageCodeChanged) ||
                                    (translationDisplayChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (bookDisplayChanged && !languageCodeChanged && !publicationCodeChanged && !bookNumberChanged) ||
                                    (chapterDisplayChanged && !languageCodeChanged && !publicationCodeChanged && !bookNumberChanged && !chapterNumberChanged);

        var cascadeChangeOccurred = languageCodeChanged || publicationCodeChanged || bookNumberChanged || chapterNumberChanged;

        return new PropertyChangeInfo
        {
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentBookNumber = currentBookNumber,
            CurrentChapterNumber = currentChapterNumber,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewTranslationDisplayText = newTranslationDisplayText,
            NewBookDisplayText = newBookDisplayText,
            NewChapterDisplayText = newChapterDisplayText,
            NotifyLanguage = notifyLanguage,
            NotifyTranslation = notifyTranslation,
            NotifyBook = notifyBook,
            NotifyChapter = notifyChapter,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            LanguageDisplayChanged = languageDisplayChanged,
            TranslationDisplayChanged = translationDisplayChanged,
            BookDisplayChanged = bookDisplayChanged,
            ChapterDisplayChanged = chapterDisplayChanged,
            HasChanges = notifyLanguage || notifyTranslation || notifyBook || notifyChapter || displayTextOnlyChanged
        };
    }

    private void UpdateLastValues(PropertyChangeInfo changeInfo)
    {
        lastLanguageCode = changeInfo.CurrentLanguageCode;
        lastPublicationCode = changeInfo.CurrentPublicationCode;
        lastBookNumber = changeInfo.CurrentBookNumber;
        lastChapterNumber = changeInfo.CurrentChapterNumber;
        lastLanguageDisplayText = changeInfo.NewLanguageDisplayText;
        lastTranslationDisplayText = changeInfo.NewTranslationDisplayText;
        lastBookDisplayText = changeInfo.NewBookDisplayText;
        lastChapterDisplayText = changeInfo.NewChapterDisplayText;
    }

    private void ResetProgressIfNeeded(ScheduleStateItem? currentSchedule, PropertyChangeInfo changeInfo)
    {
        if (changeInfo.CascadeChangeOccurred && currentSchedule != null)
        {
            var currentProgress = currentSchedule.BibleReadingFinishedDuration ?? TimeSpan.Zero;
            if (currentProgress != TimeSpan.Zero)
            {
                logger.Information("BibleSelectionContainerViewModel: OnStateChanged - Cascade change detected, resetting BibleReadingFinishedDuration from {CurrentProgress} to zero",
                    currentProgress);

                var scheduleStateItem = mapper.Map<ScheduleStateItem>(currentSchedule.DeepClone());
                scheduleStateItem.BibleReadingFinishedDuration = TimeSpan.Zero;
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, true, shouldSave: false));

                logger.Information("BibleSelectionContainerViewModel: OnStateChanged - Dispatched UpdateScheduleFromViewModelAction to reset progress. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
                    changeInfo.CurrentLanguageCode ?? "null",
                    changeInfo.CurrentPublicationCode ?? "null",
                    changeInfo.CurrentBookNumber?.ToString() ?? "null",
                    changeInfo.CurrentChapterNumber?.ToString() ?? "null");
            }
        }
    }

    private void NotifyPropertyChanges(PropertyChangeInfo changeInfo)
    {
        logger.Debug("BibleSelectionContainerViewModel: OnStateChanged - Values changed. Language: {LanguageChanged}, Translation: {TranslationChanged}, Book: {BookChanged}, Chapter: {ChapterChanged}, DisplayTextOnly: {DisplayTextOnly}. CascadeChangeOccurred: {CascadeChangeOccurred}",
            changeInfo.NotifyLanguage, changeInfo.NotifyTranslation,
            changeInfo.NotifyBook, changeInfo.NotifyChapter,
            changeInfo.DisplayTextOnlyChanged, changeInfo.CascadeChangeOccurred);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (changeInfo.NotifyLanguage)
            {
                OnPropertyChanged(nameof(LanguageDisplayText));
                OnPropertyChanged(nameof(TranslationDisplayText));
                OnPropertyChanged(nameof(BookDisplayText));
                OnPropertyChanged(nameof(ChapterDisplayText));
            }
            else if (changeInfo.NotifyTranslation)
            {
                OnPropertyChanged(nameof(TranslationDisplayText));
                OnPropertyChanged(nameof(BookDisplayText));
                OnPropertyChanged(nameof(ChapterDisplayText));
            }
            else if (changeInfo.NotifyBook)
            {
                OnPropertyChanged(nameof(BookDisplayText));
                OnPropertyChanged(nameof(ChapterDisplayText));
            }
            else if (changeInfo.NotifyChapter)
            {
                OnPropertyChanged(nameof(ChapterDisplayText));
            }
            else if (changeInfo.DisplayTextOnlyChanged)
            {
                NotifyDisplayTextOnlyChanges(changeInfo);
            }
        });
    }

    private void NotifyDisplayTextOnlyChanges(PropertyChangeInfo changeInfo)
    {
        if (changeInfo.LanguageDisplayChanged)
        {
            OnPropertyChanged(nameof(LanguageDisplayText));
        }
        if (changeInfo.TranslationDisplayChanged)
        {
            OnPropertyChanged(nameof(TranslationDisplayText));
        }
        if (changeInfo.BookDisplayChanged)
        {
            OnPropertyChanged(nameof(BookDisplayText));
        }
        if (changeInfo.ChapterDisplayChanged)
        {
            OnPropertyChanged(nameof(ChapterDisplayText));
        }
    }

    private record PropertyChangeInfo
    {
        public string? CurrentLanguageCode { get; init; }
        public string? CurrentPublicationCode { get; init; }
        public int? CurrentBookNumber { get; init; }
        public int? CurrentChapterNumber { get; init; }
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewTranslationDisplayText { get; init; } = string.Empty;
        public string NewBookDisplayText { get; init; } = string.Empty;
        public string NewChapterDisplayText { get; init; } = string.Empty;
        public bool NotifyLanguage { get; init; }
        public bool NotifyTranslation { get; init; }
        public bool NotifyBook { get; init; }
        public bool NotifyChapter { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool TranslationDisplayChanged { get; init; }
        public bool BookDisplayChanged { get; init; }
        public bool ChapterDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }

    public ICommand SelectLanguageCommand { get; private set; } = null!;
    public ICommand SelectBibleCommand { get; private set; } = null!;
    public ICommand SelectBookCommand { get; private set; } = null!;
    public ICommand SelectChapterCommand { get; private set; } = null!;

    public string LanguageDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;

            // Read from CurrentSchedule for language name (populated during bootstrap/effects)
            if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BibleReadingLanguageName))
            {
                logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - Returning '{LanguageName}' (LanguageCode: {LanguageCode})",
                    currentSchedule.BibleReadingLanguageName, currentSchedule.BibleReadingLanguageCode ?? "null");
                return currentSchedule.BibleReadingLanguageName;
            }

            logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - CurrentSchedule is null or BibleReadingLanguageName is empty. Returning empty string.");
            return string.Empty;
        }
    }

    public string TranslationDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;

            // Read from CurrentSchedule for publication name (populated during bootstrap/effects)
            if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BibleReadingPublicationName))
            {
                return currentSchedule.BibleReadingPublicationName;
            }

            // Fallback: use publication code from CurrentBibleReadingSchedule or CurrentSchedule
            var bibleReading = state.Value.CurrentBibleReadingSchedule;
            string publicationCode = bibleReading?.PublicationCode?.ToLowerInvariant()
                ?? currentSchedule?.BibleReadingPublicationCode?.ToLowerInvariant()
                ?? string.Empty;

            if (string.IsNullOrEmpty(publicationCode))
            {
                return string.Empty;
            }

            // Format publication code using helper as fallback
            return PublicationDisplayHelper.GetDisplayName(publicationCode);
        }
    }

    public string BookDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;

            // Read from CurrentSchedule for book name (populated during bootstrap/effects)
            if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BibleReadingBookName))
            {
                return currentSchedule.BibleReadingBookName;
            }

            return string.Empty;
        }
    }

    public string ChapterDisplayText
    {
        get
        {
            // Read directly from CurrentBibleReadingSchedule so it updates immediately when chapter changes
            var bibleReading = state.Value.CurrentBibleReadingSchedule;
            if (bibleReading != null && bibleReading.ChapterNumber > 0)
            {
                // ChapterNumber is always valid (1-150) if BibleReadingSchedule exists
                return $"Chapter {bibleReading.ChapterNumber}";
            }

            // Fallback to CurrentSchedule if CurrentBibleReadingSchedule is not set (e.g., new schedule)
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null &&
                currentSchedule.BibleReadingChapterNumber.HasValue &&
                currentSchedule.BibleReadingChapterNumber.Value > 0)
            {
                return $"Chapter {currentSchedule.BibleReadingChapterNumber.Value}";
            }

            return string.Empty;
        }
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

