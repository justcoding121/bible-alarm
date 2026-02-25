#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationTrackSelectionViewModel : ObservableObject, IListViewModel, IDisposable
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
    private PropertyChangedEventHandler? propertyManagerPropertyChangedHandler;
    private readonly INavigationService navigationService;
    private bool isCancelBusy;

    private readonly SemaphoreSlim @lock = new(1);

    public BiblePublicationTrackSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        IBiblePublicationService? biblePublicationService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.navigationService = navigationService;

        // Initialize helper classes
        stateManager = new TrackSelectionStateManager();
        dataProvider = new TrackSelectionDataProvider(mediaService, biblePublicationService);
        commandHandler = new TrackSelectionCommandHandler(logger, state, dispatcher, navigationService);
        propertyManager = new TrackSelectionPropertyManager();
        SetupPropertyManagerForwarding();

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
        OverlayCancelCommand = new AsyncRelayCommand(OverlayCancelAsync);

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

    private void SetupPropertyManagerForwarding()
    {
        // The view binds to THIS ViewModel (not the property manager).
        // Forward property-manager changes so bindings update (especially IsBusy for busy overlay).
        propertyManagerPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(TrackSelectionPropertyManager.IsBusy))
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        };

        propertyManager.PropertyChanged += propertyManagerPropertyChangedHandler;
    }

    private void OnBiblePublicationChanged(object? sender, EventArgs e)
    {
        stateManager.HandleBiblePublicationChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub, sectionCode) => await Initialize(lang, pub, sectionCode),
            SetSelectedTrack);
    }

    private void OnBiblePublicationInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleBiblePublicationInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub, sectionCode) => await Initialize(lang, pub, sectionCode));
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
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;
        var newPublicationCode = currentSchedule.BiblePublicationCode;
        var newSectionCode = currentSchedule.BiblePublicationSectionCode;

        // For non-sectioned publications (dramas/videos), sectionCode is 0 or null - that's valid
        // We only need language and publication codes
        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            Log.Debug("BiblePublicationTrackSelectionViewModel.RefreshFromState: Missing language or publication code, returning");
            return;
        }

        Log.Debug("BiblePublicationTrackSelectionViewModel.RefreshFromState: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
            newLanguageCode, newPublicationCode, newSectionCode ?? "(none)");

        stateManager.UpdateFromStateForNonSectioned(state);

        // Ensure tracks are populated if not already initialized
        if (!stateManager.InitComplete || propertyManager.Tracks == null || propertyManager.Tracks.Count == 0)
        {
            stateManager.SetInitComplete(true);
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = true);
            await Initialize(newLanguageCode, newPublicationCode, newSectionCode);
            SetSelectedTrack();
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
        }
        else
        {
            SetSelectedTrack();
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
        }
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

    public object? SelectedItem => propertyManager.SelectedTrack;

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
            var direction = state.Value.CurrentSchedule?.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
            return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }
    }

    private async Task Initialize(string languageCode, string publicationCode, string? sectionCode)
    {
        Log.Debug("BiblePublicationTrackSelectionViewModel.Initialize: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}, current.TrackCode={CurrentTrackCode}",
            languageCode, publicationCode, sectionCode ?? "(none)", stateManager.Current?.TrackCode ?? "(none)");

        await dataProvider.PopulateTracks(
            languageCode,
            publicationCode,
            sectionCode,
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

        if (propertyManagerPropertyChangedHandler != null)
        {
            propertyManager.PropertyChanged -= propertyManagerPropertyChangedHandler;
            propertyManagerPropertyChangedHandler = null;
        }

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

    public BiblePublicationTrack Track => track;

    private bool isSelected;
    private bool isNavigating;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsNavigating
    {
        get => isNavigating;
        set => SetProperty(ref isNavigating, value);
    }

    // LookUpPath is no longer stored in the database - it's computed at runtime by TrackMetadata
    public string TrackCode => track.TrackCode;

    /// <summary>
    /// Gets the track title with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Title => System.Net.WebUtility.HtmlDecode(track.Title).Replace('\u00A0', ' ');
    // URLs are now computed on-demand, not stored
    public string Url => string.Empty;

    public int CompareTo(object? obj) => track.CompareTo((obj as BiblePublicationTrackListViewItemModel)?.track);
}
