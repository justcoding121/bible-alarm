#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Stores.Messages.ModalOverlay;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicSectionSelectionViewModel : ObservableObject, IListViewModel, IHasFetchErrorListViewModel, IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;
    private readonly MusicInstrumentalSectionListLoader sectionListLoader;
    private readonly MusicSectionSelectionCommandHandler commandHandler;
    private bool initComplete;
    private string? lastPublicationCode;
    private string? lastSectionCode;
    private bool isDisposed;
    private bool isSelectingSection;
    private bool isInitializing;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private bool canCancelFetch = false;
    private bool hasFetchError = false;
    private bool isCancelBusy = false;
    private MusicSectionSelectionStateChangeHandler? stateChangeHandler;
    private CancellationTokenSource? fetchCts;
    private readonly MusicSectionSelectionRefreshHandler refreshHandler;
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand CancelFetchCommand { get; }

    public MusicSectionSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
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
        sectionListLoader = new MusicInstrumentalSectionListLoader(logger, mediaService);
        commandHandler = new MusicSectionSelectionCommandHandler(logger, mediaService, state, dispatcher, navigationService);
        refreshHandler = new MusicSectionSelectionRefreshHandler(logger);

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);

        TrackSelectionCommand = new AsyncRelayCommand<BiblePublicationSectionListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            // Set flag to prevent RefreshFromState from resetting IsBusy
            isSelectingSection = true;
            try
            {
                await commandHandler.HandleSectionSelectedAsync(
                    x,
                    () => isDisposed);
            }
            finally
            {
                isSelectingSection = false;
            }
        });

        // Initialize helper
        stateChangeHandler = new MusicSectionSelectionStateChangeHandler(
            logger,
            () => lastPublicationCode,
            (code) => lastPublicationCode = code,
            () => lastSectionCode,
            (code) => lastSectionCode = code,
            () => initComplete,
            (b) => IsBusy = b,
            () => Sections,
            (pub) => _ = Initialize(pub),
            SetSelectedSection);

        // Only subscribe OnMusicSectionChanged to state changes
        // OnMusicSectionInitialized will only be called once manually in the constructor
        state.StateChanged += OnMusicSectionChanged;
        WeakReferenceMessenger.Default.Register<ModalOverlayFetchProgressMessage>(this);

        // Always trigger initialization immediately to ensure we read the latest state
        // This is especially important when the modal opens after a publication change
        // This should only run once, not on every state change
        OnMusicSectionInitialized(null, EventArgs.Empty);
    }

    private void OnMusicSectionChanged(object? sender, EventArgs e)
    {
        // Don't handle state changes while selecting a section or initializing (to avoid conflicts)
        if (isDisposed || stateChangeHandler == null || isSelectingSection || isInitializing)
        {
            return;
        }
        stateChangeHandler.HandleStateChanged(state.Value);
    }

    private async Task CancelFetchAsync()
    {
        IsCancelBusy = true;
        await Task.Delay(50);

        try
        {
            logger.Information("MusicSectionSelectionViewModel: CancelFetchCommand - User cancelled fetch");
            fetchCts?.Cancel();
            CanCancelFetch = false;
            ShowProgress = false;
            IsBusy = false;
            DeviceDisplay.Current.KeepScreenOn = false;
            await navigationService.PopModalAsync();
        }
        finally
        {
            IsCancelBusy = false;
        }
    }

    private void OnMusicSectionInitialized(object? o, EventArgs eventArgs)
    {
        // This method should only be called once during construction
        // Subsequent state changes should be handled by OnMusicSectionChanged
        if (initComplete)
        {
            return;
        }

        // Always read the latest state when initializing
        // Fire-and-forget: initialization happens asynchronously, errors are handled within RefreshFromState
        // Don't initialize if we're currently selecting a section (to avoid conflicts)
        if (!isSelectingSection)
        {
            isInitializing = true;
            _ = RefreshFromState().ContinueWith(_ =>
            {
                isInitializing = false;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private async Task Initialize(string publicationCode)
    {
        // Cancel any previous fetch operation
        fetchCts?.Cancel();
        fetchCts?.Dispose();
        fetchCts = new CancellationTokenSource();

        var progressReporter = new ModalOverlayFetchProgressReporter("MusicSection", fetchCts.Token);
        await PopulateSections(publicationCode, progressReporter);
    }

    /// <summary>
    /// Refreshes the sections list from the current state. Can be called when modal appears to ensure latest state is used.
    /// </summary>
    /// <summary>
    /// Refreshes the sections list from the current state. Can be called when modal appears to ensure latest state is used.
    /// Uses a semaphore to ensure concurrent calls wait for any in-progress refresh to complete.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Don't refresh if we're currently selecting a section (to avoid conflicts with TrackSelectionCommand)
        if (isSelectingSection)
        {
            return;
        }

        // Serialize RefreshFromState calls - if a refresh is in progress, wait for it to complete
        // This fixes the race condition where fire-and-forget initialization races with ModalScrollHelper's refresh call
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
        // Prevent re-entrant calls during initialization or section selection
        if (isInitializing && initComplete)
        {
            return;
        }

        // Don't refresh if we're currently selecting a section (to avoid conflicts with TrackSelectionCommand)
        if (isSelectingSection)
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null ||
            string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var publicationCode = currentSchedule.MusicPublicationCode;
        var sectionCode = currentSchedule.MusicSectionCode;
        
        // Only handle instrumental music (no language code) for now
        // Vocal music sections use the Bible publication section selection
        // Music type is inferred: NULL/empty LanguageCode = instrumental
        var isMelodyMusic = string.IsNullOrEmpty(currentSchedule.MusicLanguageCode);
        if (!isMelodyMusic)
        {
            return;
        }

        // Check if publication code changed (need to repopulate sections)
        var publicationCodeChanged = lastPublicationCode != publicationCode;
        var needsRepopulation = publicationCodeChanged || !initComplete;

        // Update tracking variables
        lastPublicationCode = publicationCode;
        lastSectionCode = sectionCode;

        if (!initComplete)
        {
            initComplete = true;
        }

        if (needsRepopulation && !isDisposed && !isSelectingSection)
        {
            fetchCts?.Cancel();
            fetchCts?.Dispose();
            fetchCts = new CancellationTokenSource();
            var progressReporter = new ModalOverlayFetchProgressReporter("MusicSection", fetchCts.Token);
            var refreshContext = new MusicSectionSelectionRefreshContext
            {
                IsDisposed = () => isDisposed,
                IsSelectingSection = () => isSelectingSection,
                SetIsBusy = (b) => IsBusy = b,
                SetCanCancelFetch = (b) => CanCancelFetch = b,
                SetShowProgress = (b) => ShowProgress = b,
                SetProgressText = (s) => ProgressText = s,
                SetProgressPercent = (d) => ProgressPercent = d,
                SetScreenOn = (on) => DeviceDisplay.Current.KeepScreenOn = on,
                PopulateSections = (pub, progress) => PopulateSections(pub, progress),
                SetSelectedSection = SetSelectedSection
            };
            await refreshHandler.RunRepopulationAsync(refreshContext, publicationCode, progressReporter);
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!isDisposed && !isSelectingSection)
                    ShowProgress = false;
                if (!isDisposed)
                    SetSelectedSection();
            });
        }
    }

    public void Receive(ModalOverlayFetchProgressMessage message)
    {
        var p = message.Value;
        if (p.ModalType != "MusicSection")
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isDisposed && !isSelectingSection)
            {
                ProgressPercent = p.Progress;
                ProgressText = p.ProgressText;
                ShowProgress = p.IsVisible;
            }
        });
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        WeakReferenceMessenger.Default.Unregister<ModalOverlayFetchProgressMessage>(this);
        state.StateChanged -= OnMusicSectionChanged;
        fetchCts?.Cancel();
        fetchCts?.Dispose();
        refreshSemaphore.Dispose();
    }

    private void SetSelectedSection()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || Sections == null || Sections.Count == 0)
        {
            return;
        }

        if (SelectedSection != null)
        {
            SelectedSection.IsSelected = false;
        }

        // Find the section matching the current section code
        var sectionCode = currentSchedule.MusicSectionCode;
        if (string.IsNullOrEmpty(sectionCode))
        {
            return;
        }

        var section = Sections.FirstOrDefault(s =>
            s.Section.SectionCode.Equals(sectionCode, StringComparison.OrdinalIgnoreCase));
        
        if (section != null)
        {
            SelectedSection = section;
            SelectedSection.IsSelected = true;
        }
    }

    public object? SelectedItem => SelectedSection;

    public BiblePublicationSectionListViewItemModel? SelectedSection { get; set; }

    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (isBusy != value)
            {
                logger.Debug("[MusicSectionSelection] IsBusy changing from {OldValue} to {NewValue}. isSelectingSection={IsSelectingSection}, isInitializing={IsInitializing}",
                    isBusy, value, isSelectingSection, isInitializing);
            }
            SetProperty(ref isBusy, value);
        }
    }

    public bool ShowProgress
    {
        get => showProgress;
        set
        {
            if (SetProperty(ref showProgress, value))
                OnPropertyChanged(nameof(ShowCancelButton));
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
                OnPropertyChanged(nameof(ShowCancelButton));
        }
    }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    /// <summary>Show cancel button in overlay during fetch.</summary>
    public bool ShowCancelButton => ShowProgress;

    private ObservableCollection<BiblePublicationSectionListViewItemModel> sections = [];

    public ObservableCollection<BiblePublicationSectionListViewItemModel> Sections
    {
        get => sections;
        set => SetProperty(ref sections, value);
    }

    /// <summary>
    /// Gets the FlowDirection for content (always LTR for instrumental music).
    /// </summary>
    public FlowDirection ContentFlowDirection => FlowDirection.LeftToRight;

    private async Task PopulateSections(string publicationCode, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        var currentSectionCode = state.Value.CurrentSchedule?.MusicSectionCode;
        var (sectionViewModelList, selectedSection) = await sectionListLoader.LoadAsync(publicationCode, currentSectionCode, progress);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Sections.Clear();
            foreach (var section in sectionViewModelList)
            {
                Sections.Add(section);
            }

            if (selectedSection is not null)
            {
                SelectedSection = selectedSection;
            }
        });
    }
}
