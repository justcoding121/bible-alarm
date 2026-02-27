#nullable enable

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Stores.Messages.ModalOverlay;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationSectionSelectionViewModel : ObservableObject, IListViewModel, IHasFetchErrorListViewModel, IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
{
    private BiblePublicationSchedule? current;

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;
    private bool initComplete;
    private BiblePublicationSchedule? lastCurrent;
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private bool canCancelFetch = false;
    private bool hasFetchError = false;
    private bool isCancelBusy = false;
    private bool isDisposed = false;
    private bool isSelectingSection;
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState calls - ensures second call waits for first to complete
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

    private readonly StateChangeHandler stateChangeHandler;
    private readonly SectionListLoader sectionListLoader;
    private readonly TrackSelectionResolver trackSelectionResolver;
    private readonly BiblePublicationSectionSelectionTrackTapHandler trackTapHandler;
    private readonly BiblePublicationSectionSelectionRefreshHandler refreshHandler;

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand CancelFetchCommand { get; }

    public BiblePublicationSectionSelectionViewModel(ILogger logger, IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper, IInternetConnectivityChecker? internetChecker = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;
        sectionListLoader = new SectionListLoader(logger, mediaService, internetChecker);
        trackSelectionResolver = new TrackSelectionResolver(logger, mediaService);
        trackTapHandler = new BiblePublicationSectionSelectionTrackTapHandler(logger, state, dispatcher, navigationService, trackSelectionResolver);
        refreshHandler = new BiblePublicationSectionSelectionRefreshHandler(logger);

        // Don't initialize here - let OnBiblePublicationInitialized handle it
        // This ensures we always get the latest state when the modal opens
        // But we need to initialize tracking variables to null so OnBiblePublicationInitialized can detect changes

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);

        TrackSelectionCommand = new AsyncRelayCommand<BiblePublicationSectionListViewItemModel>(async (x) =>
        {
            if (x == null) return;
            isSelectingSection = true;
            try
            {
                await trackTapHandler.HandleSectionTapAsync(x);
            }
            finally
            {
                isSelectingSection = false;
            }
        });

        // Initialize helper
        stateChangeHandler = new StateChangeHandler(
            logger,
            mapper,
            () => current,
            (c) => current = c,
            (c) => lastCurrent = c,
            () => initComplete,
            (b) => IsBusy = b,
            () => Sections,
            (lang, pub) => ObserveFaultedTask(Initialize(lang, pub), "Section list Initialize from state change"),
            SetSelectedSection);

        // Only subscribe OnBiblePublicationChanged to state changes
        // OnBiblePublicationInitialized will only be called once manually in the constructor
        state.StateChanged += OnBiblePublicationChanged;

        WeakReferenceMessenger.Default.Register<ModalOverlayFetchProgressMessage>(this);

        // Always trigger initialization immediately to ensure we read the latest state
        // This is especially important when the modal opens after a language change
        // This should only run once, not on every state change
        OnBiblePublicationInitialized(null, EventArgs.Empty);
    }

    private void OnBiblePublicationChanged(object? sender, EventArgs e)
    {
        // Don't handle state changes while selecting a section (to avoid conflicts)
        if (isDisposed || isSelectingSection)
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
            logger.Information("BiblePublicationSectionSelectionViewModel: CancelFetchCommand - User cancelled fetch");
            fetchCts?.CancelAsync();
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

    private void OnBiblePublicationInitialized(object? o, EventArgs eventArgs)
    {
        // This method should only be called once during construction
        // Subsequent state changes should be handled by OnBiblePublicationChanged
        if (initComplete)
        {
            return;
        }

        // Fire-and-forget: ModalScrollHelper will also call RefreshFromState when modal appears; observe so faults are logged
        ObserveFaultedTask(RefreshFromState(), "RefreshFromState from OnBiblePublicationInitialized");
    }

    private static void ObserveFaultedTask(Task task, string context)
    {
        if (task == null) return;
        task.ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    Log.Warning(t.Exception, "BiblePublicationSectionSelectionViewModel: {Context}", context);
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

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
        // Don't refresh if we're currently selecting a section (to avoid conflicts with TrackSelectionCommand)
        if (isSelectingSection)
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBiblePublicationSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;
        var newPublicationCode = currentSchedule.BiblePublicationCode;

        // Publication code is required, but language code can be null/empty for publications without language
        if (string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // Check if language or publication code changed (need to repopulate sections)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged || !initComplete;

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Derive from CurrentSchedule (single source of truth)
        // Language code can be null/empty for publications without language
        current = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode, // Can be empty for publications without language
            PublicationCode = newPublicationCode,
            SectionCode = currentSchedule.BiblePublicationSectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };
        lastCurrent = current;

        if (!initComplete)
        {
            initComplete = true;
        }

        if (needsRepopulation && !isDisposed && !isSelectingSection)
        {
            fetchCts?.CancelAsync();
            fetchCts?.Dispose();
            fetchCts = new CancellationTokenSource();
            var progressReporter = new ModalOverlayFetchProgressReporter("BibleSection", fetchCts.Token);
            var refreshContext = new BiblePublicationSectionRefreshContext
            {
                IsDisposed = () => isDisposed,
                IsSelectingSection = () => isSelectingSection,
                SetIsBusy = (b) => IsBusy = b,
                SetCanCancelFetch = (b) => CanCancelFetch = b,
                SetShowProgress = (b) => ShowProgress = b,
                SetProgressText = (s) => ProgressText = s,
                SetProgressPercent = (d) => ProgressPercent = d,
                SetScreenOn = (on) => DeviceDisplay.Current.KeepScreenOn = on,
                Initialize = (lang, pub, progress) => Initialize(lang, pub, progress),
                SetSelectedSection = SetSelectedSection,
                SetInitCompleteFalse = () => initComplete = false
            };
            await refreshHandler.RunRepopulationAsync(refreshContext, newLanguageCode, newPublicationCode, progressReporter);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!isDisposed && !isSelectingSection)
                {
                    ShowProgress = false;
                    IsBusy = false;
                    CanCancelFetch = false;
                    SetSelectedSection();
                }
            });
        }
    }

    public void Receive(ModalOverlayFetchProgressMessage message)
    {
        var p = message.Value;
        if (p.ModalType != "BibleSection")
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isDisposed && !isSelectingSection)
            {
                if (p.IsVisible)
                {
                    ProgressPercent = p.Progress;
                    ProgressText = p.ProgressText;
                }
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
        state.StateChanged -= OnBiblePublicationChanged;
        fetchCts?.CancelAsync();
        fetchCts?.Dispose();
        refreshSemaphore.Dispose();
    }

    private void SetSelectedSection()
    {
        if (current == null || Sections == null || Sections.Count == 0)
        {
            return;
        }

        if (SelectedSection != null)
        {
            SelectedSection.IsSelected = false;
        }

        var section = SectionSelectionResolver.FindSectionToSelect(current.SectionCode, Sections);
        if (section != null)
        {
            SelectedSection = section;
            SelectedSection.IsSelected = true;
        }
    }

    public object? SelectedItem => SelectedSection;

    public BiblePublicationSectionListViewItemModel? SelectedSection { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
                OnPropertyChanged(nameof(ShowCancelButton));
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

    /// <summary>Show cancel button in overlay during fetch or when busy loading.</summary>
    public bool ShowCancelButton => ShowProgress || IsBusy;

    private ObservableCollection<BiblePublicationSectionListViewItemModel> sections = [];

    public ObservableCollection<BiblePublicationSectionListViewItemModel> Sections
    {
        get => sections;
        set => SetProperty(ref sections, value);
    }

    /// <summary>Gets the FlowDirection for content based on selected language direction.</summary>
    public FlowDirection ContentFlowDirection => ContentFlowDirectionHelper.GetContentFlowDirection(
        state.Value.CurrentSchedule?.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight);

    private async Task Initialize(string languageCode, string publicationCode, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null) => await PopulateSections(languageCode, publicationCode, progress);

    private readonly Dictionary<string, BiblePublicationSectionListViewItemModel> sectionVMsMapping = new(StringComparer.OrdinalIgnoreCase);

    private async Task PopulateSections(string languageCode, string publicationCode, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        var (sectionViewModelList, newMapping) = await sectionListLoader.LoadAsync(
            languageCode,
            publicationCode,
            current?.SectionCode,
            progress);

        // Update mapping
        sectionVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            sectionVMsMapping[kvp.Key] = kvp.Value;
        }

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Sections.Clear();
            foreach (var section in sectionViewModelList)
            {
                Sections.Add(section);
            }
        });
    }
}
