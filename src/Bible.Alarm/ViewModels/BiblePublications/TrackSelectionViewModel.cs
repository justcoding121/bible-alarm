#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.BiblePublications.TrackSelectionViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class TrackSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    // Helper classes
    private readonly TrackSelectionStateManager stateManager;
    private readonly TrackSelectionDataProvider dataProvider;
    private readonly TrackSelectionCommandHandler commandHandler;
    private readonly TrackSelectionPropertyManager propertyManager;

    private readonly SemaphoreSlim @lock = new(1);

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
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Initialize helper classes
        stateManager = new TrackSelectionStateManager(mapper);
        dataProvider = new TrackSelectionDataProvider(mediaService);
        commandHandler = new TrackSelectionCommandHandler(logger, state, dispatcher, navigationService);
        propertyManager = new TrackSelectionPropertyManager();

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        SetTrackCommand = new AsyncRelayCommand<BiblePublicationTrackListViewItemModel>(async x =>
        {
            if (x != null)
            {
                propertyManager.SelectedTrack?.IsSelected = false;
                propertyManager.SelectedTrack = x;
                propertyManager.SelectedTrack.IsSelected = true;
                await commandHandler.HandleSetTrackAsync(x);
            }
        });

        state.StateChanged += OnBiblePublicationInitialized;
        state.StateChanged += OnBiblePublicationChanged;
    }

    private void OnBiblePublicationChanged(object? sender, EventArgs e)
    {
        stateManager.HandleBiblePublicationChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub, section) => await Initialize(lang, pub, section),
            SetSelectedTrack);
    }

    private void OnBiblePublicationInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleBiblePublicationInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub, section) => await Initialize(lang, pub, section));
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures tracks are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationCode;
        var newSectionNumber = currentSchedule.BiblePublicationSectionNumber;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode) || !newSectionNumber.HasValue)
        {
            return;
        }

        stateManager.UpdateFromState(state, mapper);

        // Ensure tracks are populated if not already initialized
        if (!stateManager.InitComplete || propertyManager.Tracks == null || propertyManager.Tracks.Count == 0)
        {
            stateManager.SetInitComplete(true);
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = true);
            await Initialize(newLanguageCode, newPublicationCode, newSectionNumber.Value);
            // Set selected track after tracks are populated
            SetSelectedTrack();
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
        }
        else
        {
            // Update selected track when state changes (e.g., after navigating back)
            SetSelectedTrack();
        }
    }

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SetTrackCommand { get; set; }

    public BiblePublicationTrackListViewItemModel? SelectedTrack
    {
        get => propertyManager.SelectedTrack;
        set => propertyManager.SelectedTrack = value;
    }

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public ObservableCollection<BiblePublicationTrackListViewItemModel> Tracks
    {
        get => propertyManager.Tracks;
        set => propertyManager.Tracks = value;
    }

    /// <summary>
    /// Gets the FlowDirection for content based on the selected language direction.
    /// </summary>
    public FlowDirection ContentFlowDirection
    {
        get
        {
            var direction = state.Value.CurrentSchedule?.BiblePublicationLanguageDirection ?? "ltr";
            return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }
    }

    private async Task Initialize(string languageCode, string publicationCode, int sectionNumber)
    {
        await dataProvider.PopulateTracks(
            languageCode,
            publicationCode,
            sectionNumber,
            stateManager.Current,
            propertyManager.Tracks,
            track => propertyManager.SelectedTrack = track);
    }

    private void SetSelectedTrack()
    {
        dataProvider.SetSelectedTrack(
            stateManager.Current,
            propertyManager.Tracks,
            propertyManager.SelectedTrack,
            track => propertyManager.SelectedTrack = track);
    }

    public void Dispose()
    {
        state.StateChanged -= OnBiblePublicationInitialized;
        state.StateChanged -= OnBiblePublicationChanged;
        @lock.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class BiblePublicationTrackListViewItemModel : ObservableObject, IComparable
{
    private readonly BiblePublicationTrack track;

    public BiblePublicationTrackListViewItemModel(BiblePublicationTrack track)
    {
        this.track = track;
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    // LookUpPath is no longer stored in the database - it's computed at runtime by TrackMetadata
    public int Number => track.Number;

    /// <summary>
    /// Gets the track title with HTML entities decoded (e.g., &nbsp; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Title => System.Net.WebUtility.HtmlDecode(track.Title).Replace('\u00A0', ' ');
    public string Url => track.Source?.Url ?? string.Empty;

    public int CompareTo(object? obj) => Number.CompareTo((obj as BiblePublicationTrackListViewItemModel)?.Number);
}
