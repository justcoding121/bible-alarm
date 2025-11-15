using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class SongBookSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly MediaService _mediaService;
    private readonly IState<ApplicationState> _state;

    private AlarmMusic _current;
    private AlarmMusic _tentative;
    private bool _initComplete;
    private AlarmMusic _lastCurrent;
    private AlarmMusic _lastTentative;

    public SongBookSelectionViewModel(MediaService mediaService, IServiceScopeFactory scopeFactory, INavigationService navigationService)
    {
        _mediaService = mediaService;
        var navigationService1 = navigationService;
        _state = MauiAppHolder.Services.GetRequiredService<IState<ApplicationState>>();
        var dispatcher = MauiAppHolder.Services.GetRequiredService<IDispatcher>();

        _state.StateChanged += OnMusicInitialized;
        _state.StateChanged += OnMusicChanged;
        
        // Check current state immediately in case state is already set
        var currentState = _state.Value;
        if (currentState.CurrentMusic != null && currentState.TentativeMusic != null)
        {
            OnMusicInitialized(null, EventArgs.Empty);
        }

        TrackSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            IsBusy = true;

            await navigationService1.NavigateToTrackSelectionAsync();

            dispatcher.Dispatch(new TrackSelectionAction(new AlarmMusic
            {
                Repeat = _current.Repeat,
                MusicType = MusicType.Vocals,
                LanguageCode = CurrentLanguage.Code,
                PublicationCode = x.Code
            }));

            IsBusy = false;
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService1.OpenLanguageModalAsync(this);
            IsBusy = false;
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService1.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService1.CloseModalAsync();
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            IsBusy = true;
            if (CurrentLanguage != null) CurrentLanguage.IsSelected = false;

            CurrentLanguage = x;
            CurrentLanguage.IsSelected = true;

            await navigationService1.CloseModalAsync();
            await PopulateSongBooks(x.Code);
            IsBusy = false;
        });
    }

    private void OnMusicChanged(object sender, EventArgs e)
    {
        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null || stateValue.TentativeMusic == null ||
            (stateValue.CurrentMusic == _lastCurrent && stateValue.TentativeMusic == _lastTentative)) return;
        _current = stateValue.CurrentMusic;
        _tentative = stateValue.TentativeMusic;
        _lastCurrent = _current;
        _lastTentative = _tentative;
        
        // Update selected song book when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedSongBook);
    }

    private void OnMusicInitialized(object o, EventArgs eventArgs)
    {
        if (_initComplete) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null || stateValue.TentativeMusic == null) return;
        _current = stateValue.CurrentMusic;
        _tentative = stateValue.TentativeMusic;
        _initComplete = true;
        Task.Run(async () =>
        {
            await Initialize();
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    private void SetSelectedSongBook()
    {
        if (SelectedSongBook != null)
        {
            SelectedSongBook.IsSelected = false;
            SelectedSongBook = null;
        }

        if (_current.LanguageCode != _tentative.LanguageCode) return;
        SelectedSongBook = _songBookVMsMapping.TryGetValue(_current.PublicationCode, out var value)
            ? value
            : null;

        if (SelectedSongBook != null) SelectedSongBook.IsSelected = true;
    }

    public ICommand BackCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

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
            languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
        }

        _tentative.LanguageCode = languageCode;

        await PopulateLanguages();
        await PopulateSongBooks(languageCode);

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
                                 || x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Name))
        {
            var languageVm = new LanguageListViewItemModel(language);

            languageVMs.Add(languageVm);

            if (languageVm.Code != _tentative.LanguageCode) continue;
            languageVm.IsSelected = true;
            CurrentLanguage = languageVm;
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

            if (_current.MusicType != MusicType.Vocals
                || _current.LanguageCode != languageCode
                || _current.PublicationCode != release.Code) continue;
            SelectedSongBook = songBookListViewItemModel;
            SelectedSongBook.IsSelected = true;
        }

        SongBooks = songBookVMs;
    }

    public void Dispose()
    {
        _state.StateChanged -= OnMusicInitialized;
        _state.StateChanged -= OnMusicChanged;
    }
}