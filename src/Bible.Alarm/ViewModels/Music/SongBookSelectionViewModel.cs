using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Music;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels.Music;

public class SongBookSelectionViewModel : ViewModel, IListViewModel, IDisposable
{
    private readonly MediaService _mediaService;
    private readonly INavigationService _navigationService;
    private readonly IServiceScopeFactory _scopeFactory;

    private AlarmMusic _current;
    private AlarmMusic _tentative;

    private readonly List<IDisposable> _subscriptions = [];

    public SongBookSelectionViewModel(MediaService mediaService, INavigationService navigationService, IServiceScopeFactory scopeFactory)
    {
        _mediaService = mediaService;
        _navigationService = navigationService;
        _scopeFactory = scopeFactory;

        //set schedules from initial state.
        //this should fire only once 
        IDisposable subscription1 = null;
        subscription1 = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.CurrentMusic != null && state.TentativeMusic != null)
            {
                _current = state.CurrentMusic;
                _tentative = state.TentativeMusic;
                Task.Run(async () =>
                {
                    await Initialize();
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                });
                _subscriptions.Remove(subscription1);
                subscription1?.Dispose();
            }
        });

        _subscriptions.Add(subscription1);

        // Subscribe to subsequent music changes (skip first one)
        AlarmMusic? lastCurrent = null;
        AlarmMusic? lastTentative = null;
        var subscription2 = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.CurrentMusic != null && state.TentativeMusic != null
                && (state.CurrentMusic != lastCurrent || state.TentativeMusic != lastTentative))
            {
                _current = state.CurrentMusic;
                _tentative = state.TentativeMusic;
                lastCurrent = _current;
                lastTentative = _tentative;
            }
        });

        _subscriptions.Add(subscription2);

        TrackSelectionCommand = new Command<PublicationListViewItemModel>(async x =>
        {
            IsBusy = true;

            ReduxContainer.Store.Dispatch(new TrackSelectionAction
            {
                TentativeMusic = new AlarmMusic
                {
                    Repeat = _current.Repeat,
                    MusicType = MusicType.Vocals,
                    LanguageCode = CurrentLanguage.Code,
                    PublicationCode = x.Code
                }
            });

            using var scope = _scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<TrackSelectionViewModel>();
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

        CloseModalCommand = new Command(async () => { await _navigationService.CloseModal(); });

        SelectLanguageCommand = new Command<LanguageListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentLanguage != null) CurrentLanguage.IsSelected = false;

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
        if (viewModal.GetType() == GetType()) SetSelectedSongBook();
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
            SelectedSongBook = _songBookVMsMapping.ContainsKey(_current.PublicationCode)
                ? _songBookVMsMapping[_current.PublicationCode]
                : null;

            if (SelectedSongBook != null) SelectedSongBook.IsSelected = true;
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
                languageCode = "E";
            else
                languageCode = languages.First().Key;
        }

        _tentative.LanguageCode = languageCode;

        await PopulateLanguages();
        await PopulateSongBooks(languageCode);

        // Subscribe to LanguageSearchTerm property changes
        PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == "LanguageSearchTerm")
            {
                _ = PopulateLanguages(LanguageSearchTerm);
            }
        };
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

    private readonly Dictionary<string, PublicationListViewItemModel> _songBookVMsMapping = [];

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

        // Note: _mediaService (MediaService) is a singleton and should not be 
        // disposed here as it is managed by the DI container
    }
}