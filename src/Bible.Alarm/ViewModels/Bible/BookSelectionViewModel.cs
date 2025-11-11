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
    private readonly INavigation _navigation;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    public ICommand BackCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    private readonly List<IDisposable> _subscriptions = [];

    public BookSelectionViewModel(MediaService mediaService, INavigation navigation, IServiceProvider serviceProvider, IDispatcher dispatcher, IState<ApplicationState> state)
    {
        _mediaService = mediaService;
        _navigation = navigation;
        _serviceProvider = serviceProvider;
        _dispatcher = dispatcher;
        _state = state;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await _navigation.PopAsync();
            IsBusy = false;
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleBookListViewItemModel>(async x =>
        {
            IsBusy = true;
            _dispatcher.Dispatch(new ChapterSelectionAction(new BibleReadingSchedule
            {
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                BookNumber = x.Number
            }));

            var viewModel = _serviceProvider.GetRequiredService<ChapterSelectionViewModel>();
            var page = _serviceProvider.GetRequiredService<ChapterSelection>();
            page.BindingContext = viewModel;
            await _navigation.PushAsync(page);
            IsBusy = false;
        });

        //set schedules from initial state.
        //this should fire only once 
        EventHandler subscriptionHandler1 = null;
        subscriptionHandler1 = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentBibleReadingSchedule != null && state.TentativeBibleReadingSchedule != null)
            {
                _current = state.CurrentBibleReadingSchedule;
                _tentative = state.TentativeBibleReadingSchedule;
                Task.Run(async () =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                    await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                });
                _state.StateChanged -= subscriptionHandler1;
            }
        };
        _state.StateChanged += subscriptionHandler1;

        // Subscribe to current schedule changes (but skip first one)
        BibleReadingSchedule lastCurrent = null;
        EventHandler subscriptionHandler2 = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentBibleReadingSchedule != null && state.CurrentBibleReadingSchedule != lastCurrent)
            {
                _current = state.CurrentBibleReadingSchedule;
                lastCurrent = _current;
            }
        };
        _state.StateChanged += subscriptionHandler2;

    }

    private void SetSelectedBook()
    {
        if (_current.LanguageCode == _tentative.LanguageCode && _current.PublicationCode == _tentative.PublicationCode)
        {
            if (SelectedBook != null) SelectedBook.IsSelected = false;

            SelectedBook = _bookVMsMapping[_current.BookNumber];
            SelectedBook.IsSelected = true;
        }
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

            if (_current.LanguageCode == _tentative.LanguageCode
                && _current.PublicationCode == _tentative.PublicationCode
                && _current.BookNumber == book.Number)
            {
                bookVm.IsSelected = true;
                SelectedBook = bookVm;
            }
        }

        Books = bookVMs;
    }

    public void Dispose()
    {
        _subscriptions.ForEach(x => x.Dispose());
        
        // Note: _mediaService (MediaService) is a singleton and should not be 
        // disposed here as it is managed by the DI container
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
        return Number.CompareTo((obj as BibleBookListViewItemModel).Number);
    }
}