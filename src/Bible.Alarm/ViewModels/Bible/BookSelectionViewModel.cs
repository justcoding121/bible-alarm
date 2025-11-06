using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;

namespace Bible.Alarm.ViewModels.Bible;

public class BookSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule _current;
    private BibleReadingSchedule _tentative;

    private readonly MediaService _mediaService;
    private readonly INavigationService _navigationService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ICommand BackCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    private readonly List<IDisposable> _subscriptions = [];

    public BookSelectionViewModel(MediaService mediaService, INavigationService navigationService, IServiceScopeFactory scopeFactory)
    {
        _mediaService = mediaService;
        _navigationService = navigationService;
        _scopeFactory = scopeFactory;

        BackCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigationService.GoBack();
            IsBusy = false;
        });

        ChapterSelectionCommand = new Command<BibleBookListViewItemModel>(async x =>
        {
            IsBusy = true;
            ReduxContainer.Store.Dispatch(new ChapterSelectionAction
            {
                TentativeBibleReadingSchedule = new BibleReadingSchedule
                {
                    LanguageCode = _tentative.LanguageCode,
                    PublicationCode = _tentative.PublicationCode,
                    BookNumber = x.Number
                }
            });

            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ChapterSelectionViewModel>();
            await _navigationService.Navigate(viewModel);
            IsBusy = false;
        });

        //set schedules from initial state.
        //this should fire only once 
        IDisposable subscription1 = null;
        subscription1 = ReduxContainer.Store.Subscribe(state =>
        {
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
                _subscriptions.Remove(subscription1);
                subscription1?.Dispose();
            }
        });

        // Subscribe to current schedule changes (but skip first one)
        BibleReadingSchedule lastCurrent = null;
        var subscription2 = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.CurrentBibleReadingSchedule != null && state.CurrentBibleReadingSchedule != lastCurrent)
            {
                _current = state.CurrentBibleReadingSchedule;
                lastCurrent = _current;
            }
        });

        _subscriptions.Add(subscription1);
        _subscriptions.Add(subscription2);

        _navigationService.NavigatedBack += OnNavigated;
    }

    private void OnNavigated(object viewModal)
    {
        if (viewModal.GetType() == GetType()) SetSelectedBook();
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
        _navigationService.NavigatedBack -= OnNavigated;

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