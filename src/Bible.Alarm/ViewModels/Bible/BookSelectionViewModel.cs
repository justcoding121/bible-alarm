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

namespace Bible.Alarm.ViewModels.Bible;

public sealed class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule current;

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    private BibleReadingSchedule lastCurrent;
    
    // Track last language and publication code to detect changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;

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
            IsBusy = true;
            await navigationService.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopModalAsync();
            IsBusy = false;
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleBookListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            try
            {
                // Ensure current is set from state if it's null
                if (current == null)
                {
                    var currentItem = state.Value.CurrentBibleReadingSchedule;
                    if (currentItem != null)
                    {
                        current = mapper.Map<BibleReadingSchedule>(currentItem);
                    }
                }

                if (current == null)
                {
                    return;
                }

                // Get the first chapter for the selected book
                var chapters = await Task.Run(async () =>
                    await mediaService.GetBibleChapters(current.LanguageCode, current.PublicationCode, x.Number));

                if (chapters == null || chapters.Count == 0)
                {
                    return;
                }

                // Get the first chapter (lowest chapter number)
                var firstChapter = chapters.Values.First();
                var firstChapterNumber = firstChapter.Number;

                // Create BibleReadingStateItem with selected book and first chapter
                // IMPORTANT: Include display names from list items (no database query needed)
                // Get language name and publication name from current state (they should already be populated)
                var currentState = state.Value.CurrentBibleReadingSchedule;
                var currentSchedule = state.Value.CurrentSchedule;
                var bibleReadingItem = new BibleReadingStateItem
                {
                    LanguageCode = current.LanguageCode,
                    PublicationCode = current.PublicationCode,
                    BookNumber = x.Number,
                    ChapterNumber = firstChapterNumber,
                    // Store display names from list items and current state
                    LanguageName = currentSchedule?.BibleReadingLanguageName ?? currentState?.TranslationName,
                    PublicationName = currentSchedule?.BibleReadingPublicationName,
                    BookName = x.Name
                };

                // Dispatch ChapterSelectedAction to update CurrentBibleReadingSchedule
                // Effect will automatically sync to CurrentSchedule
                dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

                // Navigate back to schedule page
                await navigationService.PopModalAsync();
            }
            finally
            {
                IsBusy = false;
            }
        });

        // Only subscribe OnBibleReadingChanged to state changes
        // OnBibleReadingInitialized will only be called once manually in the constructor
        state.StateChanged += OnBibleReadingChanged;
        
        // Always trigger initialization immediately to ensure we read the latest state
        // This is especially important when the modal opens after a language change
        // This should only run once, not on every state change
        OnBibleReadingInitialized(null, EventArgs.Empty);
    }

    private void OnBibleReadingChanged(object sender, EventArgs e)
    {
        var stateValue = state.Value;
        
        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingChanged - CurrentSchedule is null, returning");
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;
        
        logger.Information("BookSelectionViewModel: OnBibleReadingChanged - CurrentSchedule: Id={ScheduleId}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, InitComplete: {InitComplete}",
            currentSchedule.Id,
            newLanguageCode ?? "null",
            newPublicationCode ?? "null",
            initComplete);
        
        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingChanged - LanguageCode or PublicationCode is null/empty, returning");
            return;
        }

        // Check if language or publication code changed (need to repopulate books)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged;
        
        logger.Information("BookSelectionViewModel: OnBibleReadingChanged - LanguageChanged: {LanguageChanged} ({LastLang} -> {NewLang}), PublicationChanged: {PublicationChanged} ({LastPub} -> {NewPub}), NeedsRepopulation: {NeedsRepopulation}",
            languageChanged, lastLanguageCode ?? "null", newLanguageCode,
            publicationCodeChanged, lastPublicationCode ?? "null", newPublicationCode,
            needsRepopulation);
        
        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && initComplete)
        {
            logger.Debug("BookSelectionViewModel: OnBibleReadingChanged - No changes detected, returning");
            return;
        }

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

        // If language or publication code changed, repopulate books
        if (needsRepopulation && initComplete)
        {
            logger.Information("BookSelectionViewModel: OnBibleReadingChanged - Starting repopulation with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
                newLanguageCode, newPublicationCode);
            
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                    await Initialize(newLanguageCode, newPublicationCode);
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                    
                    logger.Information("BookSelectionViewModel: OnBibleReadingChanged - Repopulation completed. Books count: {BooksCount}",
                        Books?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "BookSelectionViewModel: OnBibleReadingChanged - Error during repopulation");
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                }
            });
        }
        else
        {
            logger.Information("BookSelectionViewModel: OnBibleReadingChanged - No repopulation needed, updating selected book");
            // Update selected book when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedBook);
        }
    }

    private void OnBibleReadingInitialized(object o, EventArgs eventArgs)
    {
        // This method should only be called once during construction
        // Subsequent state changes should be handled by OnBibleReadingChanged
        if (initComplete)
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingInitialized - Already initialized, ignoring call");
            return;
        }
        
        var stateValue = state.Value;
        
        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingInitialized - CurrentSchedule is null, returning");
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;
        
        logger.Information("BookSelectionViewModel: OnBibleReadingInitialized - CurrentSchedule: Id={ScheduleId}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            currentSchedule.Id,
            newLanguageCode ?? "null",
            newPublicationCode ?? "null");
        
        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingInitialized - LanguageCode or PublicationCode is null/empty, returning");
            return;
        }
        
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
        
        initComplete = true;
        
        // Initialize with the current language/publication
        logger.Information("BookSelectionViewModel: OnBibleReadingInitialized - Starting repopulation with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
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
                
                logger.Information("BookSelectionViewModel: OnBibleReadingInitialized - Repopulation completed. Books count: {BooksCount}",
                    Books?.Count ?? 0);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow modal to continue functioning
                logger.Error(ex, "BookSelectionViewModel: OnBibleReadingInitialized - Error during repopulation");
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
        });
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

    public BibleBookListViewItemModel SelectedBook { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<BibleBookListViewItemModel> books;

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

    public int CompareTo(object obj) => obj is not BibleBookListViewItemModel other ? 1 : Number.CompareTo(other.Number);
}
