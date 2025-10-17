using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;
using Mvvmicro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels
{
    public class BookSelectionViewModel : ViewModel, IDisposable
    {
        public IContainer Container { get; set; }

        private BibleReadingSchedule _current;
        private BibleReadingSchedule _tentative;

        private MediaService _mediaService;
        private INavigationService _navigationService;

        public ICommand BackCommand { get; set; }
        public ICommand ChapterSelectionCommand { get; set; }

        private List<IDisposable> _subscriptions = new List<IDisposable>();

        public BookSelectionViewModel(IContainer container)
        {
            this.Container = container;

            this._mediaService = this.Container.Resolve<MediaService>();
            this._navigationService = this.Container.Resolve<INavigationService>();

            BackCommand = new Command(async () =>
            {
                IsBusy = true;
                await _navigationService.GoBack();
                IsBusy = false;
            });

            ChapterSelectionCommand = new Command<BibleBookListViewItemModel>(async x =>
            {
                IsBusy = true;
                ReduxContainer.Store.Dispatch(new ChapterSelectionAction()
                {
                    TentativeBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        LanguageCode = _tentative.LanguageCode,
                        PublicationCode = _tentative.PublicationCode,
                        BookNumber = x.Number
                    }
                });

                var viewModel = this.Container.Resolve<ChapterSelectionViewModel>();
                await _navigationService.Navigate(viewModel);
                IsBusy = false;
            });

            //set schedules from initial state.
            //this should fire only once 
            var subscription1 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
                   .Select(state => new { state.CurrentBibleReadingSchedule, state.TentativeBibleReadingSchedule })
                   .Where(x => x.CurrentBibleReadingSchedule != null && x.TentativeBibleReadingSchedule != null)
                   .DistinctUntilChanged()
                   .Take(1)
                   .Subscribe(async x =>
                   {
                       IsBusy = true;
                       _current = x.CurrentBibleReadingSchedule;
                       _tentative = x.TentativeBibleReadingSchedule;
                       await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                       IsBusy = false;
                   });

            //set schedules from initial state.
            //this should fire only once 
            var subscription2 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
                   .Select(state => state.CurrentBibleReadingSchedule)
                   .Where(x => x != null)
                   .DistinctUntilChanged()
                   .Subscribe(x =>
                   {
                       _current = x;
                   });

            _subscriptions.Add(subscription1);
            _subscriptions.Add(subscription2);

            _navigationService.NavigatedBack += OnNavigated;
        }

        private void OnNavigated(object viewModal)
        {
            if (viewModal.GetType() == this.GetType())
            {
                SetSelectedBook();
            }
        }

        private void SetSelectedBook()
        {
            if (_current.LanguageCode == _tentative.LanguageCode && _current.PublicationCode == _tentative.PublicationCode)
            {
                if (SelectedBook != null)
                {
                    SelectedBook.IsSelected = false;
                }

                SelectedBook = _bookVMsMapping[_current.BookNumber];
                SelectedBook.IsSelected = true;

            }
        }

        public BibleBookListViewItemModel SelectedBook { get; set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.Set(ref _isBusy, value);
        }

        private ObservableCollection<BibleBookListViewItemModel> _books;
        public ObservableCollection<BibleBookListViewItemModel> Books
        {
            get => _books;
            set => this.Set(ref _books, value);
        }

        private async Task Initialize(string languageCode, string publicationCode)
        {
            await PopulateBooks(languageCode, publicationCode);
        }

        private Dictionary<int, BibleBookListViewItemModel> _bookVMsMapping = new Dictionary<int, BibleBookListViewItemModel>();

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
            _mediaService.Dispose();

        }
    }

    public class BibleBookListViewItemModel(BibleBook book) : ViewModel, IComparable
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => this.Set(ref _isSelected, value);
        }

        public string Name => book.Name;
        public int Number => book.Number;

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleBookListViewItemModel).Number);
        }
    }
}
