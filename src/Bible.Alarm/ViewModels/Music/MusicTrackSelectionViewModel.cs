#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicTrackSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly MusicTrackStateManager stateManager;
    private readonly MusicTrackSelectionHandler selectionHandler;
    private readonly MusicTrackListManager listManager;
    private readonly MusicTrackPropertyManager propertyManager;

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
        this.state = state;
        this.dispatcher = dispatcher;

        stateManager = new MusicTrackStateManager();
        selectionHandler = new MusicTrackSelectionHandler(dispatcher, state, navigationService);
        listManager = new MusicTrackListManager(logger, mediaService);
        propertyManager = new MusicTrackPropertyManager();

        stateManager.InitializeCurrent(state);

        BackCommand = new AsyncRelayCommand(async () => await navigationService.PopAsync());
        CloseModalCommand = new AsyncRelayCommand(async () => await navigationService.PopModalAsync());

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
        if (currentState.CurrentSchedule != null && currentState.CurrentSchedule.MusicType.HasValue)
            OnMusicInitialized(null, EventArgs.Empty);
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
        string? languageCode = null;
        string? publicationCode = null;
        MusicType? musicType = null;

        for (int i = 0; i < maxWaitAttempts; i++)
        {
            var stateValue = state.Value;
            if (stateValue.CurrentSchedule != null)
            {
                musicType = stateValue.CurrentSchedule.MusicType;
                languageCode = stateValue.CurrentSchedule.MusicLanguageCode;
                publicationCode = stateValue.CurrentSchedule.MusicPublicationCode;

                if (musicType.HasValue && !string.IsNullOrEmpty(publicationCode))
                {
                    if (musicType.Value == MusicType.VocalMusic && !string.IsNullOrEmpty(languageCode))
                        break;
                    if (musicType.Value == MusicType.Music)
                        break;
                }
            }
            await Task.Delay(delayMs);
        }

        var finalStateValue = state.Value;
        if (finalStateValue.CurrentSchedule == null || !musicType.HasValue || string.IsNullOrEmpty(publicationCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }
        if (musicType.Value == MusicType.VocalMusic && string.IsNullOrEmpty(languageCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        stateManager.HandleMusicChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub) => await Initialize(lang, pub),
            () => SetSelectedTrack());

        await Task.Delay(200);
        await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
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
    public ICommand SetTrackCommand { get; set; }

    public FlowDirection ContentFlowDirection
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var direction = currentSchedule?.MusicLanguageDirection ?? "ltr";
            return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
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
        listManager.SetSelectedTrack(stateManager.Current, propertyManager.Tracks, track => propertyManager.SetSelectedTrack(track));
    }

    private async Task Initialize(string? languageCode, string publicationCode)
    {
        var musicType = stateManager.LastMusicType ?? stateManager.Current?.MusicType ?? MusicType.VocalMusic;
        var currentSectionCode = state.Value.CurrentSchedule?.MusicSectionCode;
        await listManager.PopulateTracks(musicType, languageCode, publicationCode, currentSectionCode, propertyManager.Tracks);
        foreach (var track in propertyManager.Tracks)
            listManager.SubscribeToTrackEvents(track, propertyManager.Tracks);
        listManager.SetupCollectionChangedHandler(propertyManager.Tracks);
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;
    }
}
