using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;
using Mvvmicro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public class BibleSelectionViewModel : ViewModel, IListViewModel, IDisposable
    {
        private readonly IContainer _container;

        private MediaService _mediaService;
        private INavigationService _navigationService;

        private BibleReadingSchedule _current;
        private BibleReadingSchedule _tentative;

        private List<IDisposable> _subscriptions = new List<IDisposable>();

        public ICommand BackCommand { get; set; }
        public ICommand BookSelectionCommand { get; set; }
        public ICommand OpenModalCommand { get; set; }
        public ICommand CloseModalCommand { get; set; }
        public ICommand SelectLanguageCommand { get; set; }
        public ICommand SelectSongBookCommand { get; set; }

        public BibleSelectionViewModel(IContainer container)
        {
            _container = container;

            _mediaService = _container.Resolve<MediaService>();
            _navigationService = _container.Resolve<INavigationService>();

            //set schedules from initial state.
            //this should fire only once 
            var subscription1 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
                 .Select(state => new { state.CurrentBibleReadingSchedule, state.TentativeBibleReadingSchedule })
                 .Where(x => x.CurrentBibleReadingSchedule != null && x.TentativeBibleReadingSchedule != null)
                 .DistinctUntilChanged()
                 .Take(1)
                 .Subscribe(async x =>
                 {
                     _current = x.CurrentBibleReadingSchedule;
                     _tentative = x.TentativeBibleReadingSchedule;

                     await Initialize(_tentative.LanguageCode);

                     IsBusy = false;
                 });

            _subscriptions.Add(subscription1);

            var subscription2 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
             .Select(state => new { state.CurrentBibleReadingSchedule, state.TentativeBibleReadingSchedule })
             .Where(x => x.CurrentBibleReadingSchedule != null && x.TentativeBibleReadingSchedule != null)
             .DistinctUntilChanged()
             .Skip(1)
             .Subscribe(x =>
             {
                 _current = x.CurrentBibleReadingSchedule;
                 _tentative = x.TentativeBibleReadingSchedule;
             });

            _subscriptions.Add(subscription2);

            BookSelectionCommand = new Command<PublicationListViewItemModel>(async x =>
            {
                IsBusy = true;
                ReduxContainer.Store.Dispatch(new BookSelectionAction()
                {
                    TentativeBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        PublicationCode = x.Code,
                        LanguageCode = CurrentLanguage.Code
                    }
                });
                var viewModel = _container.Resolve<BookSelectionViewModel>();
                await _navigationService.Navigate(viewModel);

                IsBusy = false;
            });

            OpenModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await _navigationService.ShowModal("LanguageModal", this);
                IsBusy = false;
            });

            BackCommand = new Command(async () =>
            {
                IsBusy = true;
                await _navigationService.GoBack();
                IsBusy = false;
            });

            CloseModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await _navigationService.CloseModal();
                IsBusy = false;
            });

            SelectLanguageCommand = new Command<LanguageListViewItemModel>(async x =>
            {
                IsBusy = true;
                if (CurrentLanguage != null)
                {
                    CurrentLanguage.IsSelected = false;
                }

                CurrentLanguage = x;
                CurrentLanguage.IsSelected = true;

                await _navigationService.CloseModal();
                await PopulateTranslations(x.Code);

                IsBusy = false;
            });

            _navigationService.NavigatedBack += OnNavigated;
        }

        private void OnNavigated(object viewModal)
        {
            if (viewModal.GetType() == GetType())
            {
                SetSelectedTranslation();
            }
        }

        private void SetSelectedTranslation()
        {
            if (_current.LanguageCode == _tentative.LanguageCode)
            {
                if (SelectedTranslation != null)
                {
                    SelectedTranslation.IsSelected = false;

                }

                SelectedTranslation = _translationVMsMapping[_current.PublicationCode];
                SelectedTranslation.IsSelected = true;
            }
        }

        private ObservableCollection<PublicationListViewItemModel> _translations;
        public ObservableCollection<PublicationListViewItemModel> Translations
        {
            get => _translations;
            set => this.Set(ref _translations, value);
        }

        private ObservableCollection<LanguageListViewItemModel> _languages;
        public ObservableCollection<LanguageListViewItemModel> Languages
        {
            get => _languages;
            set => this.Set(ref _languages, value);
        }

        public PublicationListViewItemModel SelectedTranslation { get; set; }

        private LanguageListViewItemModel _currentLanguage;
        public LanguageListViewItemModel CurrentLanguage
        {
            get => _currentLanguage;
            set => this.Set(ref _currentLanguage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.Set(ref _isBusy, value);
        }

        public string PublicationCode
        {
            get => _tentative.PublicationCode;
            set => this.Set(_tentative.PublicationCode, value);
        }

        private string _languageSearchTerm;
        public string LanguageSearchTerm
        {
            get => _languageSearchTerm;
            set => this.Set(ref _languageSearchTerm, value);
        }

        public object SelectedItem => CurrentLanguage;

        private async Task Initialize(string languageCode)
        {
            await PopulateLanguages();
            await PopulateTranslations(languageCode);

            var subscription = Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, object>>(
                                              onNextHandler => (object sender, PropertyChangedEventArgs e)
                                              => onNextHandler(new KeyValuePair<string, object>(e.PropertyName, sender)),
                                              handler => PropertyChanged += handler,
                                              handler => PropertyChanged -= handler)
                          .Where(x => x.Key == "LanguageSearchTerm")
                          .Do(async x => await PopulateLanguages(LanguageSearchTerm))
                          .Subscribe();

            _subscriptions.Add(subscription);
        }


        private async Task PopulateLanguages(string searchTerm = null)
        {
            var languages = await _mediaService.GetBibleLanguages();
            var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

            foreach (var language in languages.Select(x => x.Value)
                .Where(x => searchTerm == null
                    || x.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Name))
            {
                var languageVm = new LanguageListViewItemModel(language);

                languageVMs.Add(languageVm);

                if (languageVm.Code == _tentative.LanguageCode)
                {
                    languageVm.IsSelected = true;
                    CurrentLanguage = languageVm;
                }
            }

            Languages = languageVMs;

        }

        private Dictionary<string, PublicationListViewItemModel> _translationVMsMapping
            = new Dictionary<string, PublicationListViewItemModel>();

        private async Task PopulateTranslations(string languageCode)
        {
            _translationVMsMapping.Clear();

            var translations = await _mediaService.GetBibleTranslations(languageCode);
            var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

            foreach (var translation in translations.Select(x => x.Value))
            {
                var translationVm = new PublicationListViewItemModel(translation);

                translationVMs.Add(translationVm);
                _translationVMsMapping.Add(translationVm.Code, translationVm);

                if (_current.LanguageCode == languageCode
                    && _current.PublicationCode == translation.Code)
                {
                    translationVm.IsSelected = true;
                    SelectedTranslation = translationVm;
                }
            }

            Translations = translationVMs;
        }

        public void Dispose()
        {
            _navigationService.NavigatedBack -= OnNavigated;

            _subscriptions.ForEach(x => x.Dispose());
            _mediaService.Dispose();
        }
    }
}
