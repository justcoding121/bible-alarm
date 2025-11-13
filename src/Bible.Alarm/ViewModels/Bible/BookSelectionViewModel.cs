using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Views.Bible;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule _current;
    private BibleReadingSchedule _tentative;

    private readonly MediaService _mediaService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INavigationService _navigationService;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;
    private EventHandler _onBibleReadingChanged;
    private EventHandler _onBibleReadingInitialized;

    public ICommand BackCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    public BookSelectionViewModel(MediaService mediaService, IServiceScopeFactory scopeFactory, INavigationService navigationService)
    {
        _mediaService = mediaService;
        _scopeFactory = scopeFactory;
        _navigationService = navigationService;
        _state = MauiAppHolder.Services.GetRequiredService<IState<ApplicationState>>();
        _dispatcher = MauiAppHolder.Services.GetRequiredService<IDispatcher>();

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await _navigationService.PopAsync();
            IsBusy = false;
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleBookListViewItemModel>(async x =>
        {
            IsBusy = true;
            await _navigationService.NavigateToChapterSelectionAsync();
            _dispatcher.Dispatch(new ChapterSelectionAction(new BibleReadingSchedule
            {
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                BookNumber = x.Number
            }));
            IsBusy = false;
        });

        // Subscribe to current schedule changes (but skip first one)
        BibleReadingSchedule lastCurrent = null;

        _state.StateChanged += OnBibleReadingInitialized;
        _state.StateChanged += OnBibleReadingChanged;

        return;

        // Define handler BEFORE subscription and BEFORE any return statement
        void OnBibleReadingChanged(object sender, EventArgs e)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null ||
                stateValue.CurrentBibleReadingSchedule == lastCurrent) return;
            _current = stateValue.CurrentBibleReadingSchedule;
            lastCurrent = _current;
            
            // Update selected book when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedBook);
        }

        void OnBibleReadingInitialized(object o, EventArgs eventArgs)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null || stateValue.TentativeBibleReadingSchedule == null) return;
            _current = stateValue.CurrentBibleReadingSchedule;
            _tentative = stateValue.TentativeBibleReadingSchedule;
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });

            if (_onBibleReadingInitialized == null) return;
            _state.StateChanged -= _onBibleReadingInitialized;
            _onBibleReadingInitialized = null;
        }
    }

    public void Dispose()
    {
        if (_onBibleReadingChanged != null)
        {
            _state.StateChanged -= _onBibleReadingChanged;
            _onBibleReadingChanged = null;
        }

        if (_onBibleReadingInitialized == null) return;
        _state.StateChanged -= _onBibleReadingInitialized;
        _onBibleReadingInitialized = null;
    }

    private void SetSelectedBook()
    {
        if (_current.LanguageCode != _tentative.LanguageCode ||
            _current.PublicationCode != _tentative.PublicationCode) return;
        if (SelectedBook != null) SelectedBook.IsSelected = false;

        if (!_bookVMsMapping.TryGetValue(_current.BookNumber, out var book)) return;
        
        SelectedBook = book;
        SelectedBook.IsSelected = true;
    }

    public BibleBookListViewItemModel SelectedBook { get; set; }

    private bool _isBusy;

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

        var books = await _mediaService.GetBibleBooks(languageCode, publicationCode);
        var bookVMs = new ObservableCollection<BibleBookListViewItemModel>();

        foreach (var book in books.Select(x => x.Value))
        {
            var bookVm = new BibleBookListViewItemModel(book);

            bookVMs.Add(bookVm);
            _bookVMsMapping.Add(bookVm.Number, bookVm);

            if (_current.LanguageCode != _tentative.LanguageCode
                || _current.PublicationCode != _tentative.PublicationCode
                || _current.BookNumber != book.Number) continue;
            bookVm.IsSelected = true;
            SelectedBook = bookVm;
        }

        Books = bookVMs;
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
        if (obj is not BibleBookListViewItemModel other) return 1;
        return Number.CompareTo(other.Number);
    }
}