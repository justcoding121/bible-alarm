#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using Bible.Alarm.Stores.Messages.ModalOverlay;
namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicPublicationSelectionViewModel : ObservableObject, IListViewModel, IHasFetchErrorListViewModel, IRecipient<ListItemFetchProgressMessage>, IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Helper classes
    private readonly MusicPublicationSelectionStateManager stateManager;
    private readonly MusicPublicationSelectionDataProvider dataProvider;
    private readonly MusicPublicationSelectionCommandHandler commandHandler;
    private readonly MusicPublicationSelectionPropertyManager propertyManager;
    private readonly MusicPublicationSelectionRefreshHandler refreshHandler;
    private readonly MusicPublicationSelectionInitHandler initHandler;
    private PropertyChangedEventHandler? propertyManagerPropertyChangedHandler;

    // Cancellation support for fetch operations
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState/Initialize calls - ensures concurrent calls wait for each other
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

    public MusicPublicationSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;

        // Initialize helper classes
        stateManager = new MusicPublicationSelectionStateManager();
        var languageNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageNameService>();
        var biblePublicationService = serviceProvider.GetService<IBiblePublicationService>();
        var languageContentService = serviceProvider.GetService<ILanguageContentService>();
        dataProvider = new MusicPublicationSelectionDataProvider(mediaService, languageNameService, biblePublicationService, languageContentService, scopeFactory);
        commandHandler = new MusicPublicationSelectionCommandHandler(navigationService, state, dispatcher, mediaService, languageNameService);
        propertyManager = new MusicPublicationSelectionPropertyManager();
        refreshHandler = new MusicPublicationSelectionRefreshHandler(state, stateManager, dataProvider, propertyManager, mapper);
        initHandler = new MusicPublicationSelectionInitHandler(mediaService, stateManager, dataProvider, propertyManager);
        SetupPropertyManagerForwarding();

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;

        WeakReferenceMessenger.Default.Register<ListItemFetchProgressMessage>(this);

        // Check current state immediately in case state is already set
        // Use CurrentSchedule as source of truth
        var currentState = state.Value;
        if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.MusicPublicationCode))
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
                    stateManager.Current,
                    isVisible => propertyManager.ShowProgress = isVisible,
                    progress => propertyManager.ProgressPercent = progress,
                    text => propertyManager.ProgressText = text);
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
                    UpdateSelectedLanguage,
                    isVisible => propertyManager.ShowProgress = isVisible,
                    progress => propertyManager.ProgressPercent = progress,
                    text => propertyManager.ProgressText = text,
                    busy => propertyManager.IsBusy = busy);
            }
        });

        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        stateManager.HandleMusicChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (langCode) => await PopulateSongPublications(langCode),
            SetSelectedSongPublication);
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleMusicInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            Initialize);
    }

    private void SetSelectedSongPublication()
    {
        dataProvider.SetSelectedSongPublication(
            stateManager.Current,
            dataProvider.SongPublicationVMsMapping,
            propertyManager.SelectedSongPublication,
            songPublication => propertyManager.SelectedSongPublication = songPublication);
    }

    public ICommand BackCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }
    public ICommand CancelFetchCommand { get; }

    private async Task CancelFetchAsync()
    {
        propertyManager.IsCancelBusy = true;
        await Task.Delay(50);

        try
        {
            Serilog.Log.Information("MusicPublicationSelectionViewModel: CancelFetchCommand - User cancelled fetch");
            fetchCts?.CancelAsync();
            propertyManager.CanCancelFetch = false;
            propertyManager.ShowProgress = false;
            propertyManager.IsBusy = false;
            DeviceDisplay.Current.KeepScreenOn = false;
            await navigationService.PopModalAsync();
        }
        finally
        {
            propertyManager.IsCancelBusy = false;
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

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public ObservableCollection<PublicationListViewItemModel> SongPublications
    {
        get => propertyManager.SongPublications;
        set => propertyManager.SongPublications = value;
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

    public PublicationListViewItemModel? SelectedSongPublication
    {
        get => propertyManager.SelectedSongPublication;
        set => propertyManager.SelectedSongPublication = value;
    }

    public object? SelectedItem => propertyManager.SelectedItem;

    public bool ShowProgress
    {
        get => propertyManager.ShowProgress;
        set => propertyManager.ShowProgress = value;
    }

    public double ProgressPercent
    {
        get => propertyManager.ProgressPercent;
        set => propertyManager.ProgressPercent = value;
    }

    public string ProgressText
    {
        get => propertyManager.ProgressText;
        set => propertyManager.ProgressText = value;
    }

    public bool CanCancelFetch
    {
        get => propertyManager.CanCancelFetch;
        set => propertyManager.CanCancelFetch = value;
    }

    public bool HasFetchError
    {
        get => propertyManager.HasFetchError;
        set => propertyManager.HasFetchError = value;
    }


    public bool IsCancelBusy
    {
        get => propertyManager.IsCancelBusy;
        set => propertyManager.IsCancelBusy = value;
    }

    /// <summary>Show cancel button in overlay during fetch or when busy loading.</summary>
    public bool ShowCancelButton => ShowProgress || IsBusy;

    /// <summary>
    /// Initialize is called via Task.Run from HandleMusicInitialized.
    /// Uses semaphore to serialize with RefreshFromState calls.
    /// </summary>
    private async Task Initialize()
    {
        // Serialize with RefreshFromState calls
        await refreshSemaphore.WaitAsync();
        try
        {
            await InitializeInternal();
        }
        finally
        {
            refreshSemaphore.Release();
        }
    }

    private Task InitializeInternal() =>
        initHandler.InitializeInternalAsync(state, PopulateLanguages, PopulateSongPublications, PopulateLanguages);

    private Task PopulateLanguages(string? searchTerm = null) =>
        initHandler.PopulateLanguagesAsync(state, lang => propertyManager.CurrentLanguage = lang, searchTerm);

    /// <summary>
    /// Refreshes only the languages list for the language selection modal.
    /// </summary>
    public async Task RefreshLanguagesAsync()
    {
        await initHandler.RefreshLanguagesAsync(state, PopulateLanguages);

        // Signal that load is complete so ModalScrollHelper.WaitForNotBusyAsync returns (matches Bible language modal).
        await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures languages are populated and current is initialized from CurrentSchedule.
    /// </summary>
    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// Uses a semaphore to ensure concurrent calls (from fire-and-forget Initialize and ModalScrollHelper) wait for each other.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Serialize RefreshFromState/Initialize calls - if one is in progress, wait for it to complete
        // This fixes the race condition where fire-and-forget Initialize races with ModalScrollHelper's refresh call
        await refreshSemaphore.WaitAsync();
        try
        {
            await RefreshFromStateInternal();
        }
        finally
        {
            refreshSemaphore.Release();
        }
    }

    private async Task RefreshFromStateInternal()
    {
        fetchCts?.CancelAsync();
        fetchCts = new CancellationTokenSource();
        await refreshHandler.RefreshAsync(
            fetchCts,
            PopulateLanguages,
            PopulateSongPublications,
            SetSelectedSongPublication);

        // Signal that data load is complete so ModalScrollHelper.WaitForNotBusyAsync returns (matches Bible publication modal).
        await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
    }

    private async Task PopulateSongPublications(string? languageCode, bool downloadAll = false, IFetchProgress? progress = null, CancellationToken cancellationToken = default)
    {
        await dataProvider.PopulateSongPublications(
            languageCode,
            stateManager.Current,
            propertyManager.SongPublications,
            songPublication => propertyManager.SelectedSongPublication = songPublication,
            downloadAll,
            progress,
            cancellationToken);
    }

    private void UpdateSelectedLanguage(LanguageListViewItemModel language) =>
        initHandler.UpdateSelectedLanguage(language);

    public void Receive(ListItemFetchProgressMessage message)
    {
        var p = message.Value;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (p.Context == "MusicLanguage")
            {
                var lang = propertyManager.Languages?.FirstOrDefault(l => l.Code == p.ItemId);
                if (lang != null)
                    lang.DownloadProgress = p.Progress;
            }
            else if (p.Context == "MusicPublication")
            {
                var pub = propertyManager.SongPublications?.FirstOrDefault(pr => pr.Code == p.ItemId);
                if (pub != null)
                    pub.DownloadProgress = p.Progress;
            }
        });
    }

    public void Receive(ModalOverlayFetchProgressMessage message)
    {
        var p = message.Value;
        if (p.ModalType != "MusicPublication")
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            propertyManager.ProgressPercent = p.Progress;
            propertyManager.ProgressText = p.ProgressText;
            propertyManager.ShowProgress = p.IsVisible;
        });
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<ModalOverlayFetchProgressMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ListItemFetchProgressMessage>(this);
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;
        propertyManager.RemoveLanguageSearchHandler();

        // Cancel any ongoing fetch
        fetchCts?.CancelAsync();
        fetchCts?.Dispose();
        fetchCts = null;
        
        // Dispose semaphore
        refreshSemaphore.Dispose();

        if (propertyManagerPropertyChangedHandler != null)
        {
            propertyManager.PropertyChanged -= propertyManagerPropertyChangedHandler;
            propertyManagerPropertyChangedHandler = null;
        }
    }

    private void SetupPropertyManagerForwarding()
    {
        propertyManagerPropertyChangedHandler = MusicPublicationSelectionPropertyForwarder.CreateHandler(OnPropertyChanged);
        propertyManager.PropertyChanged += propertyManagerPropertyChangedHandler;
    }
}
