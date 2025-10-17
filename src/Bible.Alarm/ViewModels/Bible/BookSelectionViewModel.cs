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
        public IContainer container { get; set; }

        private BibleReadingSchedule current;
        private BibleReadingSchedule tentative;

        private MediaService mediaService;
        private INavigationService navigationService;

        public ICommand BackCommand { get; set; }
        public ICommand ChapterSelectionCommand { get; set; }

        private List<IDisposable> subscriptions = new List<IDisposable>();

        public BookSelectionViewModel(IContainer container)
        {
            this.container = container;

            this.mediaService = this.container.Resolve<MediaService>();
            this.navigationService = this.container.Resolve<INavigationService>();

            BackCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.GoBack();
                IsBusy = false;
            });

            ChapterSelectionCommand = new Command<BibleBookListViewItemModel>(async x =>
            {
                IsBusy = true;
                ReduxContainer.Store.Dispatch(new ChapterSelectionAction()
                {
                    TentativeBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        LanguageCode = tentative.LanguageCode,
                        PublicationCode = tentative.PublicationCode,
                        BookNumber = x.Number
                    }
                });

                var viewModel = this.container.Resolve<ChapterSelectionViewModel>();
                await navigationService.Navigate(viewModel);
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
                       current = x.CurrentBibleReadingSchedule;
                       tentative = x.TentativeBibleReadingSchedule;
                       await initialize(tentative.LanguageCode, tentative.PublicationCode);
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
                       current = x;
                   });

            subscriptions.Add(subscription1);
            subscriptions.Add(subscription2);

            navigationService.NavigatedBack += onNavigated;
        }

        private void onNavigated(object viewModal)
        {
            if (viewModal.GetType() == this.GetType())
            {
                setSelectedBook();
            }
        }

        private void setSelectedBook()
        {
            if (current.LanguageCode == tentative.LanguageCode && current.PublicationCode == tentative.PublicationCode)
            {
                if (SelectedBook != null)
                {
                    SelectedBook.IsSelected = false;
                }

                SelectedBook = bookVMsMapping[current.BookNumber];
                SelectedBook.IsSelected = true;

            }
        }

        public BibleBookListViewItemModel SelectedBook { get; set; }

        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => this.Set(ref isBusy, value);
        }

        private ObservableCollection<BibleBookListViewItemModel> books;
        public ObservableCollection<BibleBookListViewItemModel> Books
        {
            get => books;
            set => this.Set(ref books, value);
        }

        private async Task initialize(string languageCode, string publicationCode)
        {
            await populateBooks(languageCode, publicationCode);
        }

        private Dictionary<int, BibleBookListViewItemModel> bookVMsMapping = new Dictionary<int, BibleBookListViewItemModel>();

        private async Task populateBooks(string languageCode, string publicationCode)
        {
            bookVMsMapping.Clear();

            var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
            var bookVMs = new ObservableCollection<BibleBookListViewItemModel>();

            foreach (var book in books.Select(x => x.Value))
            {
                var bookVM = new BibleBookListViewItemModel(book);

                bookVMs.Add(bookVM);
                bookVMsMapping.Add(bookVM.Number, bookVM);

                if (current.LanguageCode == tentative.LanguageCode
                    && current.PublicationCode == tentative.PublicationCode
                    && current.BookNumber == book.Number)
                {
                    bookVM.IsSelected = true;
                    SelectedBook = bookVM;
                }
            }

            Books = bookVMs;
        }

        public void Dispose()
        {
            navigationService.NavigatedBack -= onNavigated;

            subscriptions.ForEach(x => x.Dispose());
            mediaService.Dispose();

        }
    }

    public class BibleBookListViewItemModel(BibleBook book) : ViewModel, IComparable
    {
        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.Set(ref isSelected, value);
        }

        public string Name => book.Name;
        public int Number => book.Number;

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleBookListViewItemModel).Number);
        }
    }
}
