#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class SongBookSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IMediaService _mediaService;
    private readonly IState<ApplicationState> _state;
    private readonly IDispatcher _dispatcher;
    private readonly INavigationService _navigationService;
    private readonly IMapper _mapper;

    private AlarmMusic? _current;
    private AlarmMusic? _tentative;
    private bool _initComplete;
    private AlarmMusic? _lastCurrent;
    private AlarmMusic? _lastTentative;
    private PropertyChangedEventHandler? _propertyChangedHandler;

    public SongBookSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        _mediaService = mediaService;
        _state = state;
        _dispatcher = dispatcher;
        _navigationService = navigationService;
        _mapper = mapper;

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
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            // Ensure _current is set from state if it's null
            if (_current == null)
            {
                var currentItem = _state.Value.CurrentMusic;
                if (currentItem != null)
                {
                    _current = _mapper.Map<AlarmMusic>(currentItem);
                }
            }

            await navigationService.NavigateToTrackSelectionAsync();

            // Map entity to DTO before dispatching
            var trackItem = new MusicStateItem
            {
                Repeat = _current?.Repeat ?? false,
                MusicType = MusicType.Vocals,
                LanguageCode = CurrentLanguage?.Code ?? string.Empty,
                PublicationCode = x.Code
            };
            _dispatcher.Dispatch(new TrackSelectionAction(trackItem));

            IsBusy = false;
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            // Ensure languages are populated before opening the modal
            if (Languages == null || Languages.Count == 0)
            {
                await PopulateLanguages();
            }

            await navigationService.OpenLanguageModalAsync(this);
            IsBusy = false;
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopAsync();
            IsBusy = false;
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopModalAsync();
            IsBusy = false;
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;
            if (CurrentLanguage != null)
            {
                CurrentLanguage.IsSelected = false;
            }

            CurrentLanguage = x;
            CurrentLanguage!.IsSelected = true;

            // Close the modal immediately after language selection
            await _navigationService.PopModalAsync();

            // Populate song books for the selected language after closing the modal
            await PopulateSongBooks(x.Code);

            IsBusy = false;
        });
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null || stateValue.TentativeMusic == null)
        {
            return;
        }

        // Map DTOs to entities
        var newCurrent = _mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
        var newTentative = _mapper.Map<AlarmMusic>(stateValue.TentativeMusic);

        // Compare by ID to avoid unnecessary updates
        if (_lastCurrent?.Id == newCurrent.Id && _lastTentative?.Id == newTentative.Id)
        {
            return;
        }

        _current = newCurrent;
        _tentative = newTentative;
        _lastCurrent = _current;
        _lastTentative = _tentative;

        // Update selected song book when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedSongBook);
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        if (_initComplete)
        {
            return;
        }

        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null || stateValue.TentativeMusic == null)
        {
            return;
        }
        // Map DTOs to entities
        _current = _mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
        _tentative = _mapper.Map<AlarmMusic>(stateValue.TentativeMusic);
        _initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize();

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following chapter/track selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    private void SetSelectedSongBook()
    {
        if (_current == null || _tentative == null || _current.LanguageCode != _tentative.LanguageCode)
        {
            return;
        }

        if (SelectedSongBook != null)
        {
            SelectedSongBook!.IsSelected = false;
        }

        if (!_songBookVMsMapping.TryGetValue(_current.PublicationCode, out var songBook))
        {
            return;
        }

        SelectedSongBook = songBook;
        SelectedSongBook!.IsSelected = true;
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

    private ObservableCollection<PublicationListViewItemModel>? _songBooks;

    public ObservableCollection<PublicationListViewItemModel> SongBooks
    {
        get => _songBooks ??= new ObservableCollection<PublicationListViewItemModel>();
        set => SetProperty(ref _songBooks, value);
    }

    private ObservableCollection<LanguageListViewItemModel>? _languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => _languages ??= new ObservableCollection<LanguageListViewItemModel>();
        set => SetProperty(ref _languages, value);
    }

    private LanguageListViewItemModel? _currentLanguage;

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => _currentLanguage;
        set => SetProperty(ref _currentLanguage, value);
    }

    private string _languageSearchTerm = string.Empty;

    public string LanguageSearchTerm
    {
        get => _languageSearchTerm;
        set => SetProperty(ref _languageSearchTerm, value);
    }

    public PublicationListViewItemModel? SelectedSongBook { get; set; }

    public object? SelectedItem => CurrentLanguage;

    private async Task Initialize()
    {
        if (_tentative == null)
        {
            return;
        }

        var languageCode = _tentative.LanguageCode;

        if (languageCode == null)
        {
            var languages = await _mediaService.GetVocalMusicLanguages();
            languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
        }

        _tentative.LanguageCode = languageCode;

        await PopulateLanguages();
        await PopulateSongBooks(languageCode);

        _propertyChangedHandler = (sender, e) =>
        {
            if (e.PropertyName == "LanguageSearchTerm")
            {
                _ = PopulateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += _propertyChangedHandler;
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        // Run database operations off UI thread
        var languages = await Task.Run(async () =>
            await _mediaService.GetVocalMusicLanguages());
        var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

        // Trim the search term before using it
        var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

        foreach (var language in languages.Select(x => x.Value)
                     .Where(x => trimmedSearchTerm == null
                                 || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Name))
        {
            var languageVm = new LanguageListViewItemModel(language);

            languageVMs.Add(languageVm);

            if (_tentative == null || languageVm.Code != _tentative.LanguageCode)
            {
                continue;
            }

            languageVm.IsSelected = true;
            CurrentLanguage = languageVm;
        }

        // Assign collection on main thread to ensure UI updates
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Languages = languageVMs;
        });
    }

    private readonly Dictionary<string, PublicationListViewItemModel> _songBookVMsMapping = [];

    private async Task PopulateSongBooks(string languageCode)
    {
        _songBookVMsMapping.Clear();

        // Run database operations off UI thread
        var songBooks = await Task.Run(async () =>
            await _mediaService.GetVocalMusicReleases(languageCode));
        var songBookVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var release in songBooks.Select(x => x.Value))
        {
            var songBookListViewItemModel = new PublicationListViewItemModel(release);

            songBookVMs.Add(songBookListViewItemModel);
            _songBookVMsMapping.Add(songBookListViewItemModel.Code, songBookListViewItemModel);

            if (_current == null
                || _current.MusicType != MusicType.Vocals
                || _current.LanguageCode != languageCode
                || _current.PublicationCode != release.Code)
            {
                continue;
            }

            songBookListViewItemModel.IsSelected = true;
            SelectedSongBook = songBookListViewItemModel;
        }

        // Assign collection on main thread to ensure UI updates before IsBusy is set to false
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            SongBooks = songBookVMs;
        });
    }

    public void Dispose()
    {
        _state.StateChanged -= OnMusicInitialized;
        _state.StateChanged -= OnMusicChanged;

        // Unsubscribe from PropertyChanged self-subscription
        if (_propertyChangedHandler is not null)
        {
            PropertyChanged -= _propertyChangedHandler;
        }
    }
}