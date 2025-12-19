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
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    private AlarmMusic? current;
    private AlarmMusic? tentative;
    private bool initComplete;
    private AlarmMusic? lastCurrent;
    private AlarmMusic? lastTentative;
    private PropertyChangedEventHandler? propertyChangedHandler;

    public SongBookSelectionViewModel(IMediaService mediaService, IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;

        this.state.StateChanged += OnMusicInitialized;
        this.state.StateChanged += OnMusicChanged;

        // Check current state immediately in case state is already set
        var currentState = this.state.Value;
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

            // Ensure current is set from state if it's null
            if (current == null)
            {
                var currentItem = state.Value.CurrentMusic;
                if (currentItem != null)
                {
                    current = mapper.Map<AlarmMusic>(currentItem);
                }
            }

            await navigationService.NavigateToTrackSelectionAsync();

            // Map entity to DTO before dispatching
            var trackItem = new MusicStateItem
            {
                Repeat = current?.Repeat ?? false,
                MusicType = MusicType.Vocals,
                LanguageCode = CurrentLanguage?.Code ?? string.Empty,
                PublicationCode = x.Code
            };
            dispatcher.Dispatch(new TrackSelectionAction(trackItem));

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
            await navigationService.PopModalAsync();

            // Populate song books for the selected language after closing the modal
            await PopulateSongBooks(x.Code);

            IsBusy = false;
        });
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentMusic == null || stateValue.TentativeMusic == null)
        {
            return;
        }

        // Map DTOs to entities
        var newCurrent = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
        var newTentative = mapper.Map<AlarmMusic>(stateValue.TentativeMusic);

        // Compare by ID to avoid unnecessary updates
        if (lastCurrent?.Id == newCurrent.Id && lastTentative?.Id == newTentative.Id)
        {
            return;
        }

        current = newCurrent;
        tentative = newTentative;
        lastCurrent = current;
        lastTentative = tentative;

        // Update selected song book when state changes (e.g., after navigating back)
        MainThread.BeginInvokeOnMainThread(SetSelectedSongBook);
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;
        if (stateValue.CurrentMusic == null || stateValue.TentativeMusic == null)
        {
            return;
        }
        // Map DTOs to entities
        current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
        tentative = mapper.Map<AlarmMusic>(stateValue.TentativeMusic);
        initComplete = true;
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
        if (current == null || tentative == null || current.LanguageCode != tentative.LanguageCode)
        {
            return;
        }

        if (SelectedSongBook != null)
        {
            SelectedSongBook!.IsSelected = false;
        }

        if (!songBookVMsMapping.TryGetValue(current.PublicationCode, out var songBook))
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

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<PublicationListViewItemModel>? songBooks;

    public ObservableCollection<PublicationListViewItemModel> SongBooks
    {
        get => songBooks ??= new ObservableCollection<PublicationListViewItemModel>();
        set => SetProperty(ref songBooks, value);
    }

    private ObservableCollection<LanguageListViewItemModel>? languages;

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= new ObservableCollection<LanguageListViewItemModel>();
        set => SetProperty(ref languages, value);
    }

    private LanguageListViewItemModel? currentLanguage;

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    private string languageSearchTerm = string.Empty;

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public PublicationListViewItemModel? SelectedSongBook { get; set; }

    public object? SelectedItem => CurrentLanguage;

    private async Task Initialize()
    {
        if (tentative == null)
        {
            return;
        }

        var languageCode = tentative.LanguageCode;

        if (languageCode == null)
        {
            var languages = await mediaService.GetVocalMusicLanguages();
            languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
        }

        tentative.LanguageCode = languageCode;

        await PopulateLanguages();
        await PopulateSongBooks(languageCode);

        propertyChangedHandler = (sender, e) =>
        {
            if (e.PropertyName == "LanguageSearchTerm")
            {
                _ = PopulateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += propertyChangedHandler;
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        // Run database operations off UI thread
        var languages = await Task.Run(async () =>
            await mediaService.GetVocalMusicLanguages());
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

            if (tentative == null || languageVm.Code != tentative.LanguageCode)
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

    private readonly Dictionary<string, PublicationListViewItemModel> songBookVMsMapping = [];

    private async Task PopulateSongBooks(string languageCode)
    {
        songBookVMsMapping.Clear();

        // Run database operations off UI thread
        var songBooks = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(languageCode));
        var songBookVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var release in songBooks.Select(x => x.Value))
        {
            var songBookListViewItemModel = new PublicationListViewItemModel(release);

            songBookVMs.Add(songBookListViewItemModel);
            songBookVMsMapping.Add(songBookListViewItemModel.Code, songBookListViewItemModel);

            if (current == null
                || current.MusicType != MusicType.Vocals
                || current.LanguageCode != languageCode
                || current.PublicationCode != release.Code)
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
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;

        // Unsubscribe from PropertyChanged self-subscription
        if (propertyChangedHandler is not null)
        {
            PropertyChanged -= propertyChangedHandler;
        }
    }
}
