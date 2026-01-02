#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.ViewModels.Bible.BookSelectionViewModelHelpers;

namespace Bible.Alarm.ViewModels.Bible;

public sealed class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule? current;

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    private BibleReadingSchedule? lastCurrent;

    // Helper class
    private readonly StateChangeHandler stateChangeHandler;

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    public BookSelectionViewModel(ILogger logger, IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Log current state when view model is created
        var currentState = state.Value;
        logger.Information("BookSelectionViewModel: Constructor - CurrentBibleReadingSchedule: {CurrentBibleReadingSchedule}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            currentState.CurrentBibleReadingSchedule != null ? "not null" : "null",
            currentState.CurrentBibleReadingSchedule?.LanguageCode ?? "null",
            currentState.CurrentBibleReadingSchedule?.PublicationCode ?? "null");

        // Don't initialize here - let OnBibleReadingInitialized handle it
        // This ensures we always get the latest state when the modal opens
        // But we need to initialize tracking variables to null so OnBibleReadingInitialized can detect changes

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleBookListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            // Always use CurrentSchedule as the source of truth for language/publication codes
            // This ensures we use the latest state, not stale data from 'current' field
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                string.IsNullOrEmpty(currentSchedule.BibleReadingLanguageCode) ||
                string.IsNullOrEmpty(currentSchedule.BibleReadingPublicationCode))
            {
                logger.Warning("BookSelectionViewModel: ChapterSelectionCommand - CurrentSchedule is null or missing required properties");
                return;
            }

            // Check if the selected book is the same as the current book
            var currentBookNumber = currentSchedule.BibleReadingBookNumber;
            var isSameBook = currentBookNumber.HasValue && currentBookNumber.Value == x.Number;

            // Get chapters for the selected book using the latest language/publication from CurrentSchedule
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(currentSchedule.BibleReadingLanguageCode, currentSchedule.BibleReadingPublicationCode, x.Number));

            if (chapters == null || chapters.Count == 0)
            {
                return;
            }

            // If it's the same book, preserve the current chapter number (if valid)
            // Otherwise, use the first chapter
            int chapterNumber;
            if (isSameBook && currentSchedule.BibleReadingChapterNumber.HasValue)
            {
                var currentChapterNumber = currentSchedule.BibleReadingChapterNumber.Value;
                // Verify the current chapter exists in the chapters list
                if (chapters.ContainsKey(currentChapterNumber))
                {
                    chapterNumber = currentChapterNumber;
                    logger.Information("BookSelectionViewModel: ChapterSelectionCommand - Same book selected ({BookName}), preserving current chapter {ChapterNumber}",
                        x.Name, chapterNumber);
                }
                else
                {
                    // Current chapter doesn't exist in this book, use first chapter
                    chapterNumber = chapters.Values.First().Number;
                    logger.Information("BookSelectionViewModel: ChapterSelectionCommand - Same book selected ({BookName}), but current chapter {CurrentChapter} doesn't exist, using first chapter {FirstChapter}",
                        x.Name, currentChapterNumber, chapterNumber);
                }
            }
            else
            {
                // Different book selected, use first chapter
                chapterNumber = chapters.Values.First().Number;
                logger.Information("BookSelectionViewModel: ChapterSelectionCommand - Different book selected ({BookName}, was {CurrentBook}), using first chapter {FirstChapter}",
                    x.Name, currentBookNumber?.ToString() ?? "null", chapterNumber);
            }

            // Create BibleReadingStateItem with selected book and chapter
            // IMPORTANT: Include display names from list items (no database query needed)
            // Get language/publication codes and display names from CurrentSchedule (they should already be populated)
            var bibleReadingItem = new BibleReadingStateItem
            {
                LanguageCode = currentSchedule.BibleReadingLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode,
                BookNumber = x.Number,
                ChapterNumber = chapterNumber,
                // Store display names from list items and current state
                LanguageName = currentSchedule.BibleReadingLanguageName,
                PublicationName = currentSchedule.BibleReadingPublicationName,
                BookName = x.Name
            };

            logger.Information("BookSelectionViewModel: ChapterSelectionCommand - Dispatching ChapterSelectedAction. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
                bibleReadingItem.LanguageCode, bibleReadingItem.PublicationCode, bibleReadingItem.BookNumber, bibleReadingItem.ChapterNumber);

            // Dispatch ChapterSelectedAction to update CurrentBibleReadingSchedule
            // Effect will automatically sync to CurrentSchedule
            dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

            // Navigate back to schedule page
            await navigationService.PopModalAsync();
        });

        // Initialize helper
        stateChangeHandler = new StateChangeHandler(
            logger,
            mapper,
            () => current,
            (c) => current = c,
            (c) => lastCurrent = c,
            () => initComplete,
            (b) => IsBusy = b,
            () => Books,
            (lang, pub) => _ = Initialize(lang, pub),
            SetSelectedBook);

        // Only subscribe OnBibleReadingChanged to state changes
        // OnBibleReadingInitialized will only be called once manually in the constructor
        state.StateChanged += OnBibleReadingChanged;

        // Always trigger initialization immediately to ensure we read the latest state
        // This is especially important when the modal opens after a language change
        // This should only run once, not on every state change
        OnBibleReadingInitialized(null, EventArgs.Empty);
    }

    private void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        stateChangeHandler.HandleStateChanged(state.Value);
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        // This method should only be called once during construction
        // Subsequent state changes should be handled by OnBibleReadingChanged
        if (initComplete)
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingInitialized - Already initialized, ignoring call");
            return;
        }

        // Always read the latest state when initializing
        RefreshFromState();
    }

    /// <summary>
    /// Refreshes the books list from the current state. Can be called when modal appears to ensure latest state is used.
    /// </summary>
    public void RefreshFromState()
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            logger.Warning("BookSelectionViewModel: RefreshFromState - CurrentSchedule is null, returning");
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;

        logger.Information("BookSelectionViewModel: RefreshFromState - CurrentSchedule: Id={ScheduleId}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, InitComplete: {InitComplete}",
            currentSchedule.Id,
            newLanguageCode ?? "null",
            newPublicationCode ?? "null",
            initComplete);

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            logger.Warning("BookSelectionViewModel: RefreshFromState - LanguageCode or PublicationCode is null/empty, returning");
            return;
        }

        // Check if language or publication code changed (need to repopulate books)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged || !initComplete;

        logger.Information("BookSelectionViewModel: RefreshFromState - LanguageChanged: {LanguageChanged} ({LastLang} -> {NewLang}), PublicationChanged: {PublicationChanged} ({LastPub} -> {NewPub}), NeedsRepopulation: {NeedsRepopulation}",
            languageChanged, lastLanguageCode ?? "null", newLanguageCode,
            publicationCodeChanged, lastPublicationCode ?? "null", newPublicationCode,
            needsRepopulation);

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentBibleReadingSchedule (for other properties like BookNumber)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                BookNumber = currentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            lastCurrent = current;
        }

        if (!initComplete)
        {
            initComplete = true;
        }

        // Initialize or repopulate with the current language/publication
        if (needsRepopulation)
        {
            logger.Information("BookSelectionViewModel: RefreshFromState - Starting repopulation with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
                newLanguageCode, newPublicationCode);

            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                    // Use the latest state values, not cached ones
                    await Initialize(newLanguageCode, newPublicationCode);

                    // Set IsBusy to false after collection is assigned - the busy overlay will hide instantly
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);

                    logger.Information("BookSelectionViewModel: RefreshFromState - Repopulation completed. Books count: {BooksCount}",
                        Books?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - allow modal to continue functioning
                    logger.Error(ex, "BookSelectionViewModel: RefreshFromState - Error during repopulation");
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                }
            });
        }
        else
        {
            logger.Information("BookSelectionViewModel: RefreshFromState - No repopulation needed, updating selected book");
            // Update selected book when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedBook);
        }
    }

    public void Dispose()
    {
        state.StateChanged -= OnBibleReadingChanged;
    }

    private void SetSelectedBook()
    {
        if (current == null)
        {
            return;
        }

        if (SelectedBook != null)
        {
            SelectedBook.IsSelected = false;
        }

        if (!bookVMsMapping.TryGetValue(current.BookNumber, out var book))
        {
            return;
        }

        SelectedBook = book;
        SelectedBook.IsSelected = true;
    }

    public BibleBookListViewItemModel? SelectedBook { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<BibleBookListViewItemModel> books = [];

    public ObservableCollection<BibleBookListViewItemModel> Books
    {
        get => books;
        set => SetProperty(ref books, value);
    }

    private async Task Initialize(string languageCode, string publicationCode) => await PopulateBooks(languageCode, publicationCode);

    private readonly Dictionary<int, BibleBookListViewItemModel> bookVMsMapping = [];

    private async Task PopulateBooks(string languageCode, string publicationCode)
    {
        logger.Information("BookSelectionViewModel: PopulateBooks - Starting with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            languageCode, publicationCode);

        bookVMsMapping.Clear();

        // Run database operations off UI thread
        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(languageCode, publicationCode));

        logger.Information("BookSelectionViewModel: PopulateBooks - Retrieved {BooksCount} books from database for LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            books?.Count ?? 0, languageCode, publicationCode);

        if (books == null)
        {
            return;
        }

        var bookVMs = new ObservableCollection<BibleBookListViewItemModel>();

        foreach (var book in books.Select(x => x.Value))
        {
            var bookVm = new BibleBookListViewItemModel(book);

            bookVMs.Add(bookVm);
            bookVMsMapping.Add(bookVm.Number, bookVm);

            if (current == null)
            {
                continue;
            }

            if (current.BookNumber != book.Number)
            {
                continue;
            }

            bookVm.IsSelected = true;
            SelectedBook = bookVm;
        }

        // Assign collection on main thread to ensure UI updates before IsBusy is set to false
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Books = bookVMs;
            logger.Information("BookSelectionViewModel: PopulateBooks - Assigned {BooksCount} books to UI. SelectedBook: {SelectedBook}",
                bookVMs.Count, SelectedBook?.Name ?? "null");
        });
    }
}

public sealed class BibleBookListViewItemModel(BibleBook book) : ObservableObject, IComparable
{
    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string Name => book.Name;
    public int Number => book.Number;

    public int CompareTo(object? obj) => obj is not BibleBookListViewItemModel other ? 1 : Number.CompareTo(other.Number);
}
