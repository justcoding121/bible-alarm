using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Shared;
using Fluxor;

namespace Bible.Alarm.ViewModels.Music;

public class SongBookSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly MediaService _mediaService;
    private readonly INavigation _navigation;
    private readonly IServiceProvider _serviceProvider;
    private readonly Fluxor.IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    private AlarmMusic _current;
    private AlarmMusic _tentative;

    private readonly List<IDisposable> _subscriptions = [];

    public SongBookSelectionViewModel(MediaService mediaService, INavigation navigation, IServiceProvider serviceProvider, Fluxor.IDispatcher dispatcher, IState<ApplicationState> state)
    {
        _mediaService = mediaService;
        _navigation = navigation;
        _serviceProvider = serviceProvider;
        _dispatcher = dispatcher;
        _state = state;

        //set schedules from initial state.
        //this should fire only once 
        EventHandler subscriptionHandler1 = null;
        subscriptionHandler1 = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentMusic != null && state.TentativeMusic != null)
            {
                _current = state.CurrentMusic;
                _tentative = state.TentativeMusic;
                Task.Run(async () =>
                {
                    await Initialize();
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                });
                _state.StateChanged -= subscriptionHandler1;
            }
        };
        _state.StateChanged += subscriptionHandler1;

        // Subscribe to subsequent music changes (skip first one)
        AlarmMusic lastCurrent = null;
        AlarmMusic lastTentative = null;
        EventHandler subscriptionHandler2 = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentMusic != null && state.TentativeMusic != null
                && (state.CurrentMusic != lastCurrent || state.TentativeMusic != lastTentative))
            {
                _current = state.CurrentMusic;
                _tentative = state.TentativeMusic;
                lastCurrent = _current;
                lastTentative = _tentative;
            }
        };
        _state.StateChanged += subscriptionHandler2;

        TrackSelectionCommand = new Command<PublicationListViewItemModel>(async x =>
        {
            IsBusy = true;

            _dispatcher.Dispatch(new TrackSelectionAction(new AlarmMusic
            {
                Repeat = _current.Repeat,
                MusicType = MusicType.Vocals,
                LanguageCode = CurrentLanguage.Code,
                PublicationCode = x.Code
            }));

            var viewModel = _serviceProvider.GetRequiredService<TrackSelectionViewModel>();
            var page = _serviceProvider.GetRequiredService<TrackSelection>();
            page.BindingContext = viewModel;
            await _navigation.PushAsync(page);

            IsBusy = false;
        });

        OpenModalCommand = new Command(async () =>
        {
            IsBusy = true;
            var modal = _serviceProvider.GetRequiredService<LanguageModal>();
            modal.BindingContext = this;
            await _navigation.PushModalAsync(modal);
            IsBusy = false;
        });

        BackCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigation.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new Command(async () =>
        {
            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
        });

        SelectLanguageCommand = new Command<LanguageListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentLanguage != null) CurrentLanguage.IsSelected = false;

            CurrentLanguage = x;
            CurrentLanguage.IsSelected = true;

            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
            await PopulateSongBooks(x.Code);
            IsBusy = false;
        });
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
        set => SetProperty(ref _isBusy, value);
    }

    private ObservableCollection<PublicationListViewItemModel> _songBooks;

    public ObservableCollection<PublicationListViewItemModel> SongBooks
    {
        get => _songBooks;
        set => SetProperty(ref _songBooks, value);
    }

    private ObservableCollection<LanguageListViewItemModel> _languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => _languages;
        set => SetProperty(ref _languages, value);
    }

    private LanguageListViewItemModel _currentLanguage;

    public LanguageListViewItemModel CurrentLanguage
    {
        get => _currentLanguage;
        set => SetProperty(ref _currentLanguage, value);
    }

    private string _languageSearchTerm;

    public string LanguageSearchTerm
    {
        get => _languageSearchTerm;
        set => SetProperty(ref _languageSearchTerm, value);
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
        _subscriptions.ForEach(x => x.Dispose());

        // Note: _mediaService (MediaService) is a singleton and should not be 
        // disposed here as it is managed by the DI container
    }
}