#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.ViewModels.Music;

public sealed partial class MusicPublicationSelectionViewModel : ObservableObject, IHasFetchErrorListViewModel, IRecipient<ListItemFetchProgressMessage>, IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
{
    private readonly IState<ApplicationState> state;
    private readonly INavigationService navigationService;

    private readonly MusicPublicationSelectionStateManager stateManager;
    private readonly MusicPublicationSelectionDataProvider dataProvider;
    private readonly MusicPublicationSelectionCommandHandler commandHandler;
    private readonly MusicPublicationSelectionRefreshHandler refreshHandler;
    private readonly MusicPublicationSelectionInitHandler initHandler;

    private bool isBusy = true;
    private ObservableCollection<PublicationListViewItemModel>? songPublications;
    private ObservableCollection<LanguageListViewItemModel>? languages;
    private LanguageListViewItemModel? currentLanguage;
    private string languageSearchTerm = string.Empty;
    private PublicationListViewItemModel? selectedSongPublication;
    private PropertyChangedEventHandler? languageSearchHandler;
    private bool showProgress;
    private double progressPercent;
    private string progressText = "0%";
    private bool canCancelFetch;
    private bool hasFetchError;
    private bool isCancelBusy;

    // Cancellation support for fetch operations
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState/Initialize calls - ensures concurrent calls wait for each other
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

    public MusicPublicationSelectionViewModel(MusicPublicationSelectionViewModelDeps deps)
    {
        state = deps.ApplicationState;
        navigationService = deps.NavigationService;

        stateManager = new MusicPublicationSelectionStateManager();
        var serviceProvider = deps.ServiceProvider;
        var languageNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageNameService>();
        var biblePublicationService = serviceProvider.GetService<IBiblePublicationService>();
        var languageContentService = serviceProvider.GetService<ILanguageContentService>();
        dataProvider = new MusicPublicationSelectionDataProvider(deps.MediaService, languageNameService, biblePublicationService, languageContentService, deps.ScopeFactory);
        commandHandler = new MusicPublicationSelectionCommandHandler(this.navigationService, state, deps.Dispatcher, deps.MediaService, languageNameService);
        refreshHandler = new MusicPublicationSelectionRefreshHandler(
            state,
            stateManager,
            () => Languages,
            () => CurrentLanguage,
            lang => CurrentLanguage = lang,
            SetupLanguageSearchHandler,
            visible => ShowProgress = visible,
            canCancel => CanCancelFetch = canCancel);
        initHandler = new MusicPublicationSelectionInitHandler(
            deps.MediaService,
            stateManager,
            dataProvider,
            () => Languages,
            () => CurrentLanguage,
            lang => CurrentLanguage = lang,
            SetupLanguageSearchHandler);

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;

        WeakReferenceMessenger.Default.Register<ListItemFetchProgressMessage>(this);
        WeakReferenceMessenger.Default.Register<ModalOverlayFetchProgressMessage>(this);

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
                await commandHandler.HandleTrackSelectionAsync(new HandleMusicPublicationTrackSelectionArgs(
                    x,
                    CurrentLanguage,
                    dataProvider,
                    stateManager.Current,
                    new TrackSelectionProgressBindings(
                        isVisible => ShowProgress = isVisible,
                        progress => ProgressPercent = progress,
                        text => ProgressText = text)));
            }
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            bool modalOpened = false;

            try
            {
                // Ensure current is set from state if it's null
                stateManager.EnsureCurrentIsSet(state);

                // Ensure languages are populated before opening the modal
                await PopulateLanguages();

                // Wait a moment to ensure the collection is assigned and UI is ready
                await Task.Delay(50);

                // Double-check that languages are populated before opening modal
                if (Languages == null || Languages.Count == 0)
                {
                    await Task.Delay(100);
                    await PopulateLanguages();
                    await Task.Delay(50);
                }

                await this.navigationService.OpenLanguageModalAsync(this);
                modalOpened = true;
            }
            finally
            {
                if (!modalOpened)
                    IsBusy = false;
            }
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await this.navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await this.navigationService.PopModalAsync();
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x != null)
            {
                await commandHandler.HandleLanguageSelectionAsync(
                    x,
                    dataProvider,
                    new HandleMusicLanguageSelectionUiCallbacks(
                        lang => CurrentLanguage = lang,
                        UpdateSelectedLanguage,
                        isVisible => ShowProgress = isVisible,
                        progress => ProgressPercent = progress,
                        text => ProgressText = text,
                        busy => IsBusy = busy));
            }
        });

        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        stateManager.HandleMusicChanged(
            state,
            busy => IsBusy = busy,
            async (langCode) => await PopulateSongPublications(langCode),
            SetSelectedSongPublication);
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleMusicInitialized(
            state,
            busy => IsBusy = busy,
            Initialize);
    }

    private void SetSelectedSongPublication()
    {
        MusicPublicationSelectionDataProvider.SetSelectedSongPublication(
            stateManager.Current,
            dataProvider.SongPublicationVMsMapping,
            SelectedSongPublication,
            songPublication => SelectedSongPublication = songPublication);
    }

    public ICommand BackCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }
    public ICommand CancelFetchCommand { get; }

    private async Task CancelFetchAsync()
    {
        IsCancelBusy = true;
        await Task.Delay(50);

        try
        {
            Serilog.Log.Information(AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.CancelFetchCommandUserCancelledFetch);
            fetchCts?.CancelAsync();
            CanCancelFetch = false;
            ShowProgress = false;
            IsBusy = false;
            DeviceDisplay.Current.KeepScreenOn = false;
            await this.navigationService.PopModalAsync();
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

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        }
    }

    public ObservableCollection<PublicationListViewItemModel> SongPublications
    {
        get => songPublications ??= [];
        set => SetProperty(ref songPublications, value);
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public PublicationListViewItemModel? SelectedSongPublication
    {
        get => selectedSongPublication;
        set => SetProperty(ref selectedSongPublication, value);
    }

    public object? SelectedItem => CurrentLanguage;

    public bool ShowProgress
    {
        get => showProgress;
        set
        {
            if (SetProperty(ref showProgress, value))
            {
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        }
    }

    public double ProgressPercent
    {
        get => progressPercent;
        set => SetProperty(ref progressPercent, value);
    }

    public string ProgressText
    {
        get => progressText;
        set => SetProperty(ref progressText, value);
    }

    public bool CanCancelFetch
    {
        get => canCancelFetch;
        set => SetProperty(ref canCancelFetch, value);
    }

    public bool HasFetchError
    {
        get => hasFetchError;
        set
        {
            if (SetProperty(ref hasFetchError, value))
            {
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        }
    }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
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
        initHandler.PopulateLanguagesAsync(state, lang => CurrentLanguage = lang, searchTerm);

    /// <summary>
    /// Refreshes only the languages list for the language selection modal.
    /// </summary>
    public async Task RefreshLanguagesAsync()
    {
        await initHandler.RefreshLanguagesAsync(state, PopulateLanguages);
    }

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
    }

    private async Task PopulateSongPublications(string? languageCode, bool downloadAll = false, IFetchProgress? progress = null, CancellationToken cancellationToken = default)
    {
        await dataProvider.PopulateSongPublications(
            languageCode,
            stateManager.Current,
            SongPublications,
            songPublication => SelectedSongPublication = songPublication,
            downloadAll,
            progress,
            cancellationToken);
    }

    private void UpdateSelectedLanguage(LanguageListViewItemModel language) =>
        initHandler.UpdateSelectedLanguage(language);

    private void SetupLanguageSearchHandler(Func<string?, Task> populateLanguages)
    {
        if (languageSearchHandler != null)
        {
            PropertyChanged -= languageSearchHandler;
        }

        languageSearchHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(LanguageSearchTerm))
            {
                _ = populateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += languageSearchHandler;
    }

    private void RemoveLanguageSearchHandler()
    {
        if (languageSearchHandler != null)
        {
            PropertyChanged -= languageSearchHandler;
            languageSearchHandler = null;
        }
    }

    public void Receive(ListItemFetchProgressMessage message)
    {
        var p = message.Value;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.Equals(p.Context, "MusicLanguage", StringComparison.Ordinal))
            {
                var lang = Languages?.FirstOrDefault(l => string.Equals(l.Code, p.ItemId, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    lang.DownloadProgress = p.Progress;
            }
            else if (string.Equals(p.Context, "MusicPublication", StringComparison.Ordinal))
            {
                var pub = SongPublications?.FirstOrDefault(pr => string.Equals(pr.Code, p.ItemId, StringComparison.OrdinalIgnoreCase));
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
            if (p.IsVisible)
            {
                ProgressPercent = p.Progress;
                ProgressText = p.ProgressText;
            }
            ShowProgress = p.IsVisible;
        });
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<ModalOverlayFetchProgressMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ListItemFetchProgressMessage>(this);
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;
        RemoveLanguageSearchHandler();

        // Cancel any ongoing fetch
        fetchCts?.CancelAsync();
        fetchCts?.Dispose();
        fetchCts = null;
        
        // Dispose semaphore
        refreshSemaphore.Dispose();
    }
}
