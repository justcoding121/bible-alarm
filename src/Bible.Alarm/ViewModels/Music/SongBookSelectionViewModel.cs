using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Music;
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
    public class SongBookSelectionViewModel : ViewModel, IListViewModel, IDisposable
    {
        private readonly IContainer _container;

        private MediaService _mediaService;
        private INavigationService _navigationService;

        private AlarmMusic _current;
        private AlarmMusic _tentative;

        private List<IDisposable> _subscriptions = new List<IDisposable>();

        public SongBookSelectionViewModel(IContainer container)
        {
            this._container = container;

            this._mediaService = this._container.Resolve<MediaService>();
            this._navigationService = this._container.Resolve<INavigationService>();

            //set schedules from initial state.
            //this should fire only once 
            var subscription1 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
                 .Select(state => new { state.CurrentMusic, state.TentativeMusic })
                 .Where(x => x.CurrentMusic != null && x.TentativeMusic != null)
                 .DistinctUntilChanged()
                 .Take(1)
                 .Subscribe(async x =>
                 {
                     _current = x.CurrentMusic;
                     _tentative = x.TentativeMusic;

                     await Initialize();

                     IsBusy = false;
                 });

            _subscriptions.Add(subscription1);

            var subscription2 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
             .Select(state => new { state.CurrentMusic, state.TentativeMusic })
             .Where(x => x.CurrentMusic != null && x.TentativeMusic != null)
             .DistinctUntilChanged()
             .Skip(1)
             .Subscribe(x =>
             {
                 _current = x.CurrentMusic;
                 _tentative = x.TentativeMusic;
             });

            _subscriptions.Add(subscription2);

            TrackSelectionCommand = new Command<PublicationListViewItemModel>(async x =>
            {
                IsBusy = true;

                ReduxContainer.Store.Dispatch(new TrackSelectionAction()
                {
                    TentativeMusic = new AlarmMusic()
                    {
                        Repeat = _current.Repeat,
                        MusicType = MusicType.Vocals,
                        LanguageCode = CurrentLanguage.Code,
                        PublicationCode = x.Code
                    }
                });

                var viewModel = this._container.Resolve<TrackSelectionViewModel>();
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
                await _navigationService.CloseModal();
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
                await PopulateSongBooks(x.Code);
                IsBusy = false;
            });

            _navigationService.NavigatedBack += OnNavigated;
        }

        private void OnNavigated(object viewModal)
        {
            if (viewModal.GetType() == this.GetType())
            {
                SetSelectedSongBook();
            }
        }

        private void SetSelectedSongBook()
        {
            if (SelectedSongBook != null)
            {
                SelectedSongBook.IsSelected = false;
                SelectedSongBook = null;
            }

            if (_current.LanguageCode == _tentative.LanguageCode)
            {
                SelectedSongBook = _songBookVMsMapping.ContainsKey(_current.PublicationCode) ? _songBookVMsMapping[_current.PublicationCode] : null;

                if (SelectedSongBook != null)
                {
                    SelectedSongBook.IsSelected = true;
                }
            }
        }

        public ICommand BackCommand { get; set; }
        public ICommand TrackSelectionCommand { get; set; }
        public ICommand OpenModalCommand { get; set; }
        public ICommand CloseModalCommand { get; set; }
        public ICommand SelectLanguageCommand { get; set; }
        public ICommand SelectSongBookCommand { get; set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.Set(ref _isBusy, value);
        }

        private ObservableCollection<PublicationListViewItemModel> _songBooks;
        public ObservableCollection<PublicationListViewItemModel> SongBooks
        {
            get => _songBooks;
            set => this.Set(ref _songBooks, value);
        }

        private ObservableCollection<LanguageListViewItemModel> _languages;
        public ObservableCollection<LanguageListViewItemModel> Languages
        {
            get => _languages;
            set => this.Set(ref _languages, value);
        }

        private LanguageListViewItemModel _currentLanguage;
        public LanguageListViewItemModel CurrentLanguage
        {
            get => _currentLanguage;
            set => this.Set(ref _currentLanguage, value);
        }

        private string _languageSearchTerm;
        public string LanguageSearchTerm
        {
            get => _languageSearchTerm;
            set => this.Set(ref _languageSearchTerm, value);
        }

        public PublicationListViewItemModel SelectedSongBook { get; set; }

        public object SelectedItem => CurrentLanguage;
        private async Task Initialize()
        {
            var languageCode = _tentative.LanguageCode;

            if (languageCode == null)
            {
                var languages = await _mediaService.GetVocalMusicLanguages();
                if (languages.ContainsKey("E"))
                {
                    languageCode = "E";
                }
                else
                {
                    languageCode = languages.First().Key;
                }
            }

            _tentative.LanguageCode = languageCode;

            await PopulateLanguages();
            await PopulateSongBooks(languageCode);

            var subscription1 = Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, object>>(
                                              onNextHandler => (object sender, PropertyChangedEventArgs e)
                                              => onNextHandler(new KeyValuePair<string, object>(e.PropertyName, sender)),
                                              handler => PropertyChanged += handler,
                                              handler => PropertyChanged -= handler)
                          .Where(x => x.Key == "LanguageSearchTerm")
                          .Do(async x => await PopulateLanguages(LanguageSearchTerm))
                          .Subscribe();

            _subscriptions.Add(subscription1);
        }

        private async Task PopulateLanguages(string searchTerm = null)
        {
            var languages = await _mediaService.GetVocalMusicLanguages();
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

        private Dictionary<string, PublicationListViewItemModel> _songBookVMsMapping
            = new Dictionary<string, PublicationListViewItemModel>();

        private async Task PopulateSongBooks(string languageCode)
        {
            SelectedSongBook = null;

            _songBookVMsMapping.Clear();

            var songBooks = await _mediaService.GetVocalMusicReleases(languageCode);
            var songBookVMs = new ObservableCollection<PublicationListViewItemModel>();

            foreach (var release in songBooks.Select(x => x.Value))
            {
                var songBookListViewItemModel = new PublicationListViewItemModel(release);

                songBookVMs.Add(songBookListViewItemModel);
                _songBookVMsMapping.Add(songBookListViewItemModel.Code, songBookListViewItemModel);

                if (_current.MusicType == MusicType.Vocals
                    && _current.LanguageCode == languageCode
                    && _current.PublicationCode == release.Code)
                {
                    SelectedSongBook = songBookListViewItemModel;
                    SelectedSongBook.IsSelected = true;
                }
            }

            SongBooks = songBookVMs;
        }

        public void Dispose()
        {
            _navigationService.NavigatedBack -= OnNavigated;
            _subscriptions.ForEach(x => x.Dispose());

            _mediaService.Dispose();
        }
    }
}
