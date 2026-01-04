#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Music.TrackSelectionViewModelHelpers;
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
        stateManager = new TrackStateManager(mapper);
        selectionHandler = new TrackSelectionHandler(logger, dispatcher, state, mapper, navigationService);
        listManager = new TrackListManager(logger, mediaService);
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

        // Check current state immediately in case state is already set
        // Also check CurrentSchedule as fallback (source of truth for new schedules)
        var currentState = state.Value;
        if (currentState.CurrentMusic != null || 
            (currentState.CurrentSchedule != null && currentState.CurrentSchedule.MusicType.HasValue))
        {
            OnMusicInitialized(null, EventArgs.Empty);
        }
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
        {
            await Initialize(stateManager.Current.LanguageCode, stateManager.Current.PublicationCode);
        }
    }

    /// <summary>
    /// Refreshes the tracks list from the current state. Can be called when modal appears to ensure latest state is used.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var musicType = currentSchedule.MusicType;
        var languageCode = currentSchedule.MusicLanguageCode;
        var publicationCode = currentSchedule.MusicPublicationCode;

        if (!musicType.HasValue || string.IsNullOrEmpty(publicationCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        // For vocals, language code is required
        if (musicType.Value == MusicType.Vocals && string.IsNullOrEmpty(languageCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        // Update state manager tracking
        stateManager.HandleMusicChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub) => await Initialize(lang, pub),
            () => SetSelectedTrack());
        
        // Ensure IsBusy is set to false after a short delay to allow async operations to complete
        // This handles cases where HandleMusicChanged returns early or async work completes quickly
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
        var musicType = stateManager.LastMusicType ?? stateManager.Current?.MusicType ?? MusicType.Vocals;
        await listManager.PopulateTracks(musicType, languageCode, publicationCode, propertyManager.Tracks);

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
