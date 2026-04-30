#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicTrackSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly MusicTrackStateManager stateManager;
    private readonly MusicTrackSelectionHandler selectionHandler;
    private readonly MusicTrackListManager listManager;
    private readonly MusicTrackPropertyManager propertyManager;
    private PropertyChangedEventHandler? propertyManagerPropertyChangedHandler;
    private readonly INavigationService navigationService;
    private bool isCancelBusy;

    public MusicTrackSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.navigationService = navigationService;

        stateManager = new MusicTrackStateManager();
        selectionHandler = new MusicTrackSelectionHandler(dispatcher, state, this.navigationService);
        listManager = new MusicTrackListManager(logger, mediaService);
        propertyManager = new MusicTrackPropertyManager();
        SetupPropertyManagerForwarding();

        stateManager.InitializeCurrent(state);

        BackCommand = new AsyncRelayCommand(async () => await this.navigationService.PopAsync());
        CloseModalCommand = new AsyncRelayCommand(async () => await this.navigationService.PopModalAsync());
        OverlayCancelCommand = new AsyncRelayCommand(OverlayCancelAsync);

        SetTrackCommand = new AsyncRelayCommand<MusicTrackListViewItemModel>(async x =>
        {
            if (x == null) return;
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

        var currentState = state.Value;
        if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.MusicPublicationCode))
            OnMusicInitialized(null, EventArgs.Empty);
    }

    private void SetupPropertyManagerForwarding()
    {
        // The view binds to THIS ViewModel (not the property manager).
        // Forward property-manager changes so bindings update (especially IsBusy for busy overlay).
        propertyManagerPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(MusicTrackPropertyManager.IsBusy))
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        };

        propertyManager.PropertyChanged += propertyManagerPropertyChangedHandler;
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
        if (stateManager.Current != null && !string.IsNullOrEmpty(stateManager.Current.PublicationCode))
            await Initialize(stateManager.Current.LanguageCode, stateManager.Current.PublicationCode);
    }

    public async Task RefreshFromState()
    {
        const int maxWaitAttempts = 10;
        const int delayMs = 100;
        string? publicationCode = null;

        for (int i = 0; i < maxWaitAttempts; i++)
        {
            var stateValue = state.Value;
            if (stateValue.CurrentSchedule != null)
            {
                publicationCode = stateValue.CurrentSchedule.MusicPublicationCode;

                // Only need a valid publication code to proceed
                if (!string.IsNullOrEmpty(publicationCode))
                    break;
            }
            await Task.Delay(delayMs);
        }

        var finalStateValue = state.Value;
        if (finalStateValue.CurrentSchedule == null || string.IsNullOrEmpty(publicationCode))
        {
            return;
        }

        stateManager.InitializeCurrent(state);

        var languageCode = finalStateValue.CurrentSchedule.MusicLanguageCode;
        var pubCode = finalStateValue.CurrentSchedule.MusicPublicationCode ?? string.Empty;
        var currentSectionCode = finalStateValue.CurrentSchedule.MusicSectionCode;
        var isSectionedPub = !string.IsNullOrEmpty(pubCode) && PublicationTypeHelper.HasSectionStructure(pubCode);
        var sectionChanged = isSectionedPub && currentSectionCode != stateManager.LastLoadedSectionCode;

        if (!stateManager.InitComplete || propertyManager.Tracks == null || propertyManager.Tracks.Count == 0 || sectionChanged)
        {
            await Initialize(languageCode, pubCode);
            SetSelectedTrack();
        }
        else
        {
            SetSelectedTrack();
        }
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleMusicInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            InitializeTracks,
            () => SetSelectedTrack());
    }

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand OverlayCancelCommand { get; set; }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    public ICommand SetTrackCommand { get; set; }

    private async Task OverlayCancelAsync()
    {
        IsCancelBusy = true;
        await Task.Delay(50);
        try
        {
            await navigationService.PopModalAsync();
        }
        finally
        {
            IsCancelBusy = false;
        }
    }

    public FlowDirection ContentFlowDirection
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var direction = currentSchedule?.MusicLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
            return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }
    }

    public object? SelectedItem => propertyManager.SelectedTrack;

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
        MusicTrackListManager.SetSelectedTrack(stateManager.Current, propertyManager.Tracks, track => propertyManager.SetSelectedTrack(track));
    }

    private async Task Initialize(string? languageCode, string publicationCode)
    {
        // Determine melody vs vocal by checking the publication in the media index,
        // not by languageCode (which can be set to "E" by the reducer for display purposes).
        var isMelodyMusic = await mediaService.IsPublicationWithoutLanguageAsync(publicationCode);
        var currentSectionCode = state.Value.CurrentSchedule?.MusicSectionCode;
        await listManager.PopulateTracks(isMelodyMusic, languageCode, publicationCode, currentSectionCode, propertyManager.Tracks);
        stateManager.SetLastLoadedSection(currentSectionCode);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var tracksSnapshot = propertyManager.Tracks.ToList();
            foreach (var track in tracksSnapshot)
                listManager.SubscribeToTrackEvents(track, propertyManager.Tracks);
            listManager.SetupCollectionChangedHandler(propertyManager.Tracks);
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;

        if (propertyManagerPropertyChangedHandler != null)
        {
            propertyManager.PropertyChanged -= propertyManagerPropertyChangedHandler;
            propertyManagerPropertyChangedHandler = null;
        }

        listManager.TeardownCollectionChangedHandler();
    }
}
