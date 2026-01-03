#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music.SongBookSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class SongBookSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    // Helper classes
    private readonly SongBookSelectionStateManager stateManager;
    private readonly SongBookSelectionDataProvider dataProvider;
    private readonly SongBookSelectionCommandHandler commandHandler;
    private readonly SongBookSelectionPropertyManager propertyManager;

    public SongBookSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;

        // Initialize helper classes
        stateManager = new SongBookSelectionStateManager(mapper);
        dataProvider = new SongBookSelectionDataProvider(mediaService);
        commandHandler = new SongBookSelectionCommandHandler(navigationService, state, dispatcher);
        propertyManager = new SongBookSelectionPropertyManager();

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;

        // Check current state immediately in case state is already set
        var currentState = state.Value;
        if (currentState.CurrentMusic != null)
        {
            OnMusicInitialized(null, EventArgs.Empty);
        }

        TrackSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x != null)
            {
                await commandHandler.HandleTrackSelectionAsync(
                    x,
                    propertyManager.CurrentLanguage,
                    dataProvider,
                    stateManager.Current);
            }
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            propertyManager.IsBusy = true;

            try
            {
                // Ensure current is set from state if it's null
                stateManager.EnsureCurrentIsSet(state, mapper);

                // Ensure languages are populated before opening the modal
                await PopulateLanguages();

                // Wait a moment to ensure the collection is assigned and UI is ready
                await Task.Delay(50);

                // Double-check that languages are populated before opening modal
                if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
                {
                    await Task.Delay(100);
                    await PopulateLanguages();
                    await Task.Delay(50);
                }

                await navigationService.OpenLanguageModalAsync(this);
            }
            finally
            {
                propertyManager.IsBusy = false;
            }
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x != null)
            {
                await commandHandler.HandleLanguageSelectionAsync(
                    x,
                    dataProvider,
                    lang => propertyManager.CurrentLanguage = lang,
                    UpdateSelectedLanguage);
            }
        });
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        stateManager.HandleMusicChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (langCode) => await PopulateSongBooks(langCode),
            SetSelectedSongBook);
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleMusicInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            Initialize);
    }

    private void SetSelectedSongBook()
    {
        dataProvider.SetSelectedSongBook(
            stateManager.Current,
            dataProvider.SongBookVMsMapping,
            propertyManager.SelectedSongBook,
            songBook => propertyManager.SelectedSongBook = songBook);
    }

    public ICommand BackCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public ObservableCollection<PublicationListViewItemModel> SongBooks
    {
        get => propertyManager.SongBooks;
        set => propertyManager.SongBooks = value;
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => propertyManager.Languages;
        set => propertyManager.Languages = value;
    }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => propertyManager.CurrentLanguage;
        set => propertyManager.CurrentLanguage = value;
    }

    public string LanguageSearchTerm
    {
        get => propertyManager.LanguageSearchTerm;
        set => propertyManager.LanguageSearchTerm = value;
    }

    public PublicationListViewItemModel? SelectedSongBook
    {
        get => propertyManager.SelectedSongBook;
        set => propertyManager.SelectedSongBook = value;
    }

    public object? SelectedItem => propertyManager.SelectedItem;

    private async Task Initialize()
    {
        var current = stateManager.Current;
        if (current == null)
        {
            return;
        }

        var languageCode = current.LanguageCode;

        if (languageCode == null)
        {
            var languages = await mediaService.GetVocalMusicLanguages();
            languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key ?? "E";
            current.LanguageCode = languageCode;
        }

        await PopulateLanguages();
        await PopulateSongBooks(languageCode);

        propertyManager.SetupLanguageSearchHandler(async (searchTerm) => await PopulateLanguages(searchTerm));
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        await dataProvider.PopulateLanguages(
            stateManager.Current,
            propertyManager.Languages,
            lang => propertyManager.CurrentLanguage = lang,
            searchTerm);
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures languages are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null || !stateValue.CurrentSchedule.MusicType.HasValue)
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        var current = stateManager.GetCurrentFromState(state, mapper);
        if (current != null)
        {
            stateManager.EnsureCurrentIsSet(state, mapper);
        }

        // Ensure languages are populated
        if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
        {
            await PopulateLanguages();
        }

        await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
    }

    private async Task PopulateSongBooks(string languageCode)
    {
        await dataProvider.PopulateSongBooks(
            languageCode,
            stateManager.Current,
            propertyManager.SongBooks,
            songBook => propertyManager.SelectedSongBook = songBook);
    }

    private void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        if (propertyManager.CurrentLanguage != null)
        {
            propertyManager.CurrentLanguage.IsSelected = false;
        }

        propertyManager.CurrentLanguage = language;
        propertyManager.CurrentLanguage!.IsSelected = true;
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;
        propertyManager.RemoveLanguageSearchHandler();
    }
}
