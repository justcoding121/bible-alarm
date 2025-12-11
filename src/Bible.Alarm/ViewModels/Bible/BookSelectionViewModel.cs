using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule _current;
    private BibleReadingSchedule _tentative;

    private readonly IMediaService _mediaService;
    private readonly IState<ApplicationState> _state;
    private readonly IDispatcher _dispatcher;
    private readonly IMapper _mapper;
    private bool _initComplete;
    private BibleReadingSchedule _lastCurrent;

    public ICommand BackCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    public BookSelectionViewModel(IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        _mediaService = mediaService;
        _state = state;
        _dispatcher = dispatcher;
        _mapper = mapper;

        // Initialize _current and _tentative from state if available (map DTOs to entities)
        var currentState = _state.Value;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            _current = _mapper.Map<BibleReadingSchedule>(currentState.CurrentBibleReadingSchedule);
        }
        if (currentState.TentativeBibleReadingSchedule != null)
        {
            _tentative = _mapper.Map<BibleReadingSchedule>(currentState.TentativeBibleReadingSchedule);
        }

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopAsync();
            IsBusy = false;
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleBookListViewItemModel>(async x =>
        {
            if (x == null) return;
            
            IsBusy = true;

            // Ensure _tentative is set from state if it's null
            if (_tentative == null)
            {
                var tentativeItem = _state.Value.TentativeBibleReadingSchedule;
                if (tentativeItem != null)
                {
                    _tentative = _mapper.Map<BibleReadingSchedule>(tentativeItem);
                }
            }

            if (_tentative == null)
            {
                IsBusy = false;
                return;
            }

            await navigationService.NavigateToChapterSelectionAsync();
            // Map entity to DTO before dispatching
            var chapterSelectionItem = new BibleReadingStateItem
            {
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                BookNumber = x.Number
            };
            _dispatcher.Dispatch(new ChapterSelectionAction(chapterSelectionItem));
            IsBusy = false;
        });

        _state.StateChanged += OnBibleReadingInitialized;
        _state.StateChanged += OnBibleReadingChanged;
    }

    private void OnBibleReadingChanged(object sender, EventArgs e)
    {
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null) return;
        
        // Map DTO to entity
        var newCurrent = _mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        // Compare by ID to avoid unnecessary updates
        if (_lastCurrent?.Id == newCurrent.Id) return;
        
        _current = newCurrent;
        _lastCurrent = _current;
        
        // Update selected book when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedBook);
    }

    private void OnBibleReadingInitialized(object o, EventArgs eventArgs)
    {
        if (_initComplete) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null) return;
        // Map DTOs to entities
        _current = _mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        _tentative = _mapper.Map<BibleReadingSchedule>(stateValue.TentativeBibleReadingSchedule);
        _initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
            
            // Set IsBusy to false after collection is assigned - the busy overlay will hide instantly
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    public void Dispose()
    {
        _state.StateChanged -= OnBibleReadingChanged;
        _state.StateChanged -= OnBibleReadingInitialized;
    }

    private void SetSelectedBook()
    {
        if (_current == null || _tentative == null) return;
        if (_current.LanguageCode != _tentative.LanguageCode ||
            _current.PublicationCode != _tentative.PublicationCode) return;
        if (SelectedBook != null) SelectedBook.IsSelected = false;

        if (!_bookVMsMapping.TryGetValue(_current.BookNumber, out var book)) return;
        
        SelectedBook = book;
        SelectedBook.IsSelected = true;
    }

    public BibleBookListViewItemModel SelectedBook { get; set; }

    // Start as true to show busy indicator immediately
    private bool _isBusy = true;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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
            await _mediaService.GetBibleBooks(languageCode, publicationCode));
        var bookVMs = new ObservableCollection<BibleBookListViewItemModel>();

        foreach (var book in books.Select(x => x.Value))
        {
            var bookVm = new BibleBookListViewItemModel(book);

            bookVMs.Add(bookVm);
            _bookVMsMapping.Add(bookVm.Number, bookVm);

            if (_current == null || _tentative == null) continue;
            if (_current.LanguageCode != _tentative.LanguageCode
                || _current.PublicationCode != _tentative.PublicationCode
                || _current.BookNumber != book.Number) continue;
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
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Name => book.Name;
    public int Number => book.Number;

    public int CompareTo(object obj)
    {
        return obj is not BibleBookListViewItemModel other ? 1 : Number.CompareTo(other.Number);
    }
}