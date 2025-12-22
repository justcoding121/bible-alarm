using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public sealed class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule current;

    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    private BibleReadingSchedule lastCurrent;

    public ICommand BackCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    public BookSelectionViewModel(IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Initialize current from state if available (map DTO to entity)
        var currentState = state.Value;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(currentState.CurrentBibleReadingSchedule);
        }

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopAsync();
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
                var bibleReadingItem = new BibleReadingStateItem
                {
                    LanguageCode = current.LanguageCode,
                    PublicationCode = current.PublicationCode,
                    BookNumber = x.Number,
                    ChapterNumber = firstChapterNumber
                };

                // Dispatch ChapterSelectedAction to update CurrentBibleReadingSchedule
                dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

                // Navigate back to schedule page
                await navigationService.PopAsync();
            }
            finally
            {
                IsBusy = false;
            }
        });

        state.StateChanged += OnBibleReadingInitialized;
        state.StateChanged += OnBibleReadingChanged;
    }

    private void OnBibleReadingChanged(object sender, EventArgs e)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null)
        {
            return;
        }

        // Map DTO to entity
        var newCurrent = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        // Compare by ID to avoid unnecessary updates
        if (lastCurrent?.Id == newCurrent.Id)
        {
            return;
        }

        current = newCurrent;
        lastCurrent = current;

        // Update selected book when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedBook);
    }

    private void OnBibleReadingInitialized(object o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null)
        {
            return;
        }
        // Map DTO to entity
        current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(current.LanguageCode, current.PublicationCode);

            // Set IsBusy to false after collection is assigned - the busy overlay will hide instantly
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnBibleReadingChanged;
        state.StateChanged -= OnBibleReadingInitialized;
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
        bookVMsMapping.Clear();

        // Run database operations off UI thread
        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(languageCode, publicationCode));
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
