using System.Collections.ObjectModel;
using System.Windows.Input;
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
    private readonly IState<ApplicationState> _state;
    private EventHandler _onBibleReadingChanged;
    private EventHandler _onBibleReadingInitialized;

    public ICommand BackCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    public BookSelectionViewModel(MediaService mediaService, INavigation navigation, IServiceProvider serviceProvider, IDispatcher dispatcher, IState<ApplicationState> state)
    {
        _mediaService = mediaService;
        var navigation1 = navigation;
        var serviceProvider1 = serviceProvider;
        var dispatcher1 = dispatcher;
        _state = state;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigation1.PopAsync();
            IsBusy = false;
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleBookListViewItemModel>(async x =>
        {
            IsBusy = true;
            dispatcher1.Dispatch(new ChapterSelectionAction(new BibleReadingSchedule
            {
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                BookNumber = x.Number
            }));

            var viewModel = serviceProvider1.GetRequiredService<ChapterSelectionViewModel>();
            var page = serviceProvider1.GetRequiredService<ChapterSelection>();
            page.BindingContext = viewModel;
            await navigation1.PushAsync(page);
            IsBusy = false;
        });

        //set schedules from initial state.
        //this should fire only once 
        _onBibleReadingInitialized = (sender, e) =>
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null ||
                stateValue.TentativeBibleReadingSchedule == null) return;
            _current = stateValue.CurrentBibleReadingSchedule;
            _tentative = stateValue.TentativeBibleReadingSchedule;
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });
            if (_onBibleReadingInitialized != null)
            {
                _state.StateChanged -= _onBibleReadingInitialized;
                _onBibleReadingInitialized = null;
            }
        };
        _state.StateChanged += _onBibleReadingInitialized;

        // Subscribe to current schedule changes (but skip first one)
        BibleReadingSchedule lastCurrent = null;

        void OnBibleReadingChanged(object sender, EventArgs e)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentBibleReadingSchedule == null ||
                stateValue.CurrentBibleReadingSchedule == lastCurrent) return;
            _current = stateValue.CurrentBibleReadingSchedule;
            lastCurrent = _current;
        }

        _onBibleReadingChanged = OnBibleReadingChanged;
        _state.StateChanged += _onBibleReadingChanged;

    }

    public void Dispose()
    {
        if (_onBibleReadingChanged != null)
        {
            _state.StateChanged -= _onBibleReadingChanged;
            _onBibleReadingChanged = null;
        }
        if (_onBibleReadingInitialized != null)
        {
            _state.StateChanged -= _onBibleReadingInitialized;
            _onBibleReadingInitialized = null;
        }
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