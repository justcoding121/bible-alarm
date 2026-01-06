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
    private string? lastLanguageCode;
    private string? lastPublicationCode;

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
                }
                else
                {
                    // Current chapter doesn't exist in this book, use first chapter
                    chapterNumber = chapters.Values.First().Number;
                }
            }
            else
            {
                // Different book selected, use first chapter
                chapterNumber = chapters.Values.First().Number;
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
            return;
        }

        // Always read the latest state when initializing
        // Fire-and-forget: initialization happens asynchronously, errors are handled within RefreshFromState
        _ = RefreshFromState();
    }

    /// <summary>
    /// Refreshes the books list from the current state. Can be called when modal appears to ensure latest state is used.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // Check if language or publication code changed (need to repopulate books)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged || !initComplete;

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
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                // Use the latest state values, not cached ones
                await Initialize(newLanguageCode, newPublicationCode);

                // Set selected book after books are populated (on main thread to ensure UI is ready)
                await MainThread.InvokeOnMainThreadAsync(() => SetSelectedBook());

                // Give CollectionView time to render before hiding busy indicator
                // This matches the pattern used in ChapterSelectionViewModel
                await Task.Delay(100);

                // Set IsBusy to false after collection is assigned and rendered - the busy overlay will hide instantly
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow modal to continue functioning
                logger.Error(ex, "BookSelectionViewModel: RefreshFromState - Error during repopulation");
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
        }
        else
        {
            // Update selected book when state changes (e.g., after navigating back)
            // Ensure this runs on main thread for UI updates
            MainThread.BeginInvokeOnMainThread(() => SetSelectedBook());
        }
    }

    public void Dispose()
    {
        state.StateChanged -= OnBibleReadingChanged;
    }

    private void SetSelectedBook()
    {
        if (current == null || Books == null || Books.Count == 0)
        {
            return;
        }

        if (SelectedBook != null)
        {
            SelectedBook.IsSelected = false;
        }

        // Find the book from the Books collection (same instance as in ItemsSource)
        // This matches the pattern used in ChapterSelectionViewModel
        var book = Books.FirstOrDefault(b => b.Number == current.BookNumber);
        if (book != null)
        {
            SelectedBook = book;
            SelectedBook.IsSelected = true;
        }
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
        bookVMsMapping.Clear();

        // Run database operations off UI thread
        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(languageCode, publicationCode));

        if (books == null)
        {
            return;
        }

        // Build the list of book view models
        var bookViewModelList = new List<BibleBookListViewItemModel>();
        BibleBookListViewItemModel? selectedBook = null;

        foreach (var book in books.Select(x => x.Value))
        {
            var bookVm = new BibleBookListViewItemModel(book);

            bookViewModelList.Add(bookVm);
            bookVMsMapping.Add(bookVm.Number, bookVm);

            if (current is null)
            {
                continue;
            }

            if (current.BookNumber != book.Number)
            {
                continue;
            }

            selectedBook = bookVm;
            selectedBook.IsSelected = true;
        }

        // Assign the complete collection on main thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Books.Clear();
            foreach (var book in bookViewModelList)
            {
                Books.Add(book);
            }

            if (selectedBook is not null)
            {
                SelectedBook = selectedBook;
            }
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
