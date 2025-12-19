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

public class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule current;
    private BibleReadingSchedule tentative;

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
        mediaService = mediaService;
        state = state;
        dispatcher = dispatcher;
        mapper = mapper;

        // Initialize current and tentative from state if available (map DTOs to entities)
        var currentState = state.Value;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(currentState.CurrentBibleReadingSchedule);
        }
        if (currentState.TentativeBibleReadingSchedule != null)
        {
            tentative = mapper.Map<BibleReadingSchedule>(currentState.TentativeBibleReadingSchedule);
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

            // Ensure tentative is set from state if it's null
            if (tentative == null)
            {
                var tentativeItem = state.Value.TentativeBibleReadingSchedule;
                if (tentativeItem != null)
                {
                    tentative = mapper.Map<BibleReadingSchedule>(tentativeItem);
                }
            }

            if (tentative == null)
            {
                IsBusy = false;
                return;
            }

            await navigationService.NavigateToChapterSelectionAsync();
            // Map entity to DTO before dispatching
            var chapterSelectionItem = new BibleReadingStateItem
            {
                LanguageCode = tentative.LanguageCode,
                PublicationCode = tentative.PublicationCode,
                BookNumber = x.Number
            };
            dispatcher.Dispatch(new ChapterSelectionAction(chapterSelectionItem));
            IsBusy = false;
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
        if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null)
        {
            return;
        }
        // Map DTOs to entities
        current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        tentative = mapper.Map<BibleReadingSchedule>(stateValue.TentativeBibleReadingSchedule);
        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(tentative.LanguageCode, tentative.PublicationCode);

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
        if (current == null || tentative == null)
        {
            return;
        }

        if (current.LanguageCode != tentative.LanguageCode ||
            current.PublicationCode != tentative.PublicationCode)
        {
            return;
        }

        if (SelectedBook != null)
        {
            SelectedBook.IsSelected = false;
        }

        if (!_bookVMsMapping.TryGetValue(current.BookNumber, out var book))
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

    private ObservableCollection<BibleBookListViewItemModel> _books;

    public ObservableCollection<BibleBookListViewItemModel> Books
    {
        get => _books;
        set => SetProperty(ref _books, value);
    }

    private async Task Initialize(string languageCode, string publicationCode)
    {
        await PopulateBooks(languageCode, publicationCode);
    }

    private readonly Dictionary<int, BibleBookListViewItemModel> _bookVMsMapping = [];

    private async Task PopulateBooks(string languageCode, string publicationCode)
    {
        _bookVMsMapping.Clear();

        // Run database operations off UI thread
        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(languageCode, publicationCode));
        var bookVMs = new ObservableCollection<BibleBookListViewItemModel>();

        foreach (var book in books.Select(x => x.Value))
        {
            var bookVm = new BibleBookListViewItemModel(book);

            bookVMs.Add(bookVm);
            _bookVMsMapping.Add(bookVm.Number, bookVm);

            if (current == null || tentative == null)
            {
                continue;
            }

            if (current.LanguageCode != tentative.LanguageCode
                || current.PublicationCode != tentative.PublicationCode
                || current.BookNumber != book.Number)
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

public class BibleBookListViewItemModel(BibleBook book) : ObservableObject, IComparable
{
    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string Name => book.Name;
    public int Number => book.Number;

    public int CompareTo(object obj)
    {
        return obj is not BibleBookListViewItemModel other ? 1 : Number.CompareTo(other.Number);
    }
}
