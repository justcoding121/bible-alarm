#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Music.TrackSelectionHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class TrackSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    // Helper classes
    private readonly TrackStateManager stateManager;
    private readonly TrackSelectionHandler selectionHandler;
    private readonly TrackListManager listManager;
    private readonly TrackPropertyManager propertyManager;

    public TrackSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;

        // Initialize helper classes
        stateManager = new TrackStateManager(logger, mapper);
        selectionHandler = new TrackSelectionHandler(logger, dispatcher, state, mapper, navigationService);
        listManager = new TrackListManager(logger, mediaService, urlRefreshService, downloadService);
        propertyManager = new TrackPropertyManager();

        stateManager.InitializeCurrent(state);

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        SetTrackCommand = new AsyncRelayCommand<MusicTrackListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            if (SelectedTrack != null)
            {
                SelectedTrack.IsSelected = false;
                SelectedTrack.Repeat = false;
            }

            SelectedTrack = x;
            SelectedTrack.IsSelected = true;

            await selectionHandler.HandleTrackSelection(x, stateManager.Current);
        });

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        stateManager.HandleMusicChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub) => await Initialize(lang, pub),
            () => SetSelectedTrack());
    }

    private async Task InitializeTracks()
    {
        if (stateManager.Current != null && !string.IsNullOrEmpty(stateManager.Current.LanguageCode))
        {
            await Initialize(stateManager.Current.LanguageCode, stateManager.Current.PublicationCode);
        }
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleMusicInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            InitializeTracks);
    }


    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SetTrackCommand { get; set; }

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public ObservableCollection<MusicTrackListViewItemModel> Tracks => propertyManager.Tracks;

    public MusicTrackListViewItemModel? SelectedTrack
    {
        get => propertyManager.SelectedTrack;
        set => propertyManager.SelectedTrack = value;
    }

    private void SetSelectedTrack()
    {
        listManager.SetSelectedTrack(stateManager.Current, propertyManager.Tracks, track => propertyManager.SetSelectedTrack(track));
    }

    private async Task Initialize(string languageCode, string publicationCode)
    {
        await listManager.PopulateTracks(languageCode, publicationCode, propertyManager.Tracks);

        // Subscribe to PropertyChanged events for Repeat
        foreach (var track in propertyManager.Tracks)
        {
            listManager.SubscribeToTrackEvents(track, propertyManager.Tracks);
        }

        // Subscribe to collection changes to handle new items
        listManager.SetupCollectionChangedHandler(propertyManager.Tracks);
    }



    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;
    }
}
