#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
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

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicSectionSelectionViewModel : ObservableObject, IListViewModel, IDisposable
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
    private MusicType? lastMusicType;
    private string? lastSectionCode;
    private bool isDisposed;
    private bool isSelectingSection;
    private bool isInitializing;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = "0%";
    private MusicSectionSelectionStateChangeHandler? stateChangeHandler;
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState calls - ensures second call waits for first to complete
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

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        CancelFetchCommand = new AsyncRelayCommand(async () =>
        {
            logger.Information("MusicSectionSelectionViewModel: CancelFetchCommand - User cancelled fetch");
            fetchCts?.Cancel();
            ShowProgress = false;
            IsBusy = false;
            // Allow screen to turn off when user cancels
            DeviceDisplay.Current.KeepScreenOn = false;
            await navigationService.PopModalAsync();
        });

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
            () => lastMusicType,
            (type) => lastMusicType = type,
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
        
        // Create progress tracker for state change handler (when publication/music type changes)
        var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
            progress => _ = MainThread.InvokeOnMainThreadAsync(() => 
            {
                if (!isDisposed)
                {
                    ProgressPercent = progress;
                }
            }),
            text => _ = MainThread.InvokeOnMainThreadAsync(() => 
            {
                if (!isDisposed)
                {
                    ProgressText = text;
                }
            }),
            isVisible => _ = MainThread.InvokeOnMainThreadAsync(() => 
            {
                if (!isDisposed)
                {
                    ShowProgress = isVisible;
                }
            }),
            fetchCts.Token);
        
        await PopulateSections(publicationCode, progressTracker);
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
            !stateValue.CurrentSchedule.MusicType.HasValue ||
            string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var musicType = currentSchedule.MusicType.Value;
        var publicationCode = currentSchedule.MusicPublicationCode;
        var sectionCode = currentSchedule.MusicSectionCode;

        // Only handle instrumental music (Music) for now
        // Vocal music sections use the Bible publication section selection
        if (musicType != MusicType.Music)
        {
            return;
        }

        // Check if publication code or music type changed (need to repopulate sections)
        var publicationCodeChanged = lastPublicationCode != publicationCode;
        var musicTypeChanged = lastMusicType != musicType;
        var needsRepopulation = publicationCodeChanged || musicTypeChanged || !initComplete;

        // Update tracking variables
        lastPublicationCode = publicationCode;
        lastMusicType = musicType;
        lastSectionCode = sectionCode;

        if (!initComplete)
        {
            initComplete = true;
        }

        // Initialize or repopulate with the current publication
        // Don't repopulate if we're currently selecting a section (to avoid conflicts)
        if (needsRepopulation && !isDisposed && !isSelectingSection)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => 
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        IsBusy = true;
                        // Keep screen on during download to prevent Android from restricting network access
                        DeviceDisplay.Current.KeepScreenOn = true;
                    }
                });
                
                if (isDisposed || isSelectingSection)
                {
                    return;
                }
                
                // Cancel any previous fetch operation
                fetchCts?.Cancel();
                fetchCts?.Dispose();
                fetchCts = new CancellationTokenSource();
                
                // Create progress tracker for modal open with async UI updates (fire-and-forget tasks to avoid blocking)
                var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                    progress => _ = MainThread.InvokeOnMainThreadAsync(() => 
                    {
                        if (!isDisposed && !isSelectingSection)
                        {
                            ProgressPercent = progress;
                        }
                    }),
                    text => _ = MainThread.InvokeOnMainThreadAsync(() => 
                    {
                        if (!isDisposed && !isSelectingSection)
                        {
                            ProgressText = text;
                        }
                    }),
                    isVisible => _ = MainThread.InvokeOnMainThreadAsync(() => 
                    {
                        if (!isDisposed && !isSelectingSection)
                        {
                            ShowProgress = isVisible;
                        }
                    }),
                    fetchCts.Token);
                
                // Use the latest state values, not cached ones
                await PopulateSections(publicationCode, progressTracker);

                // Check again if we're selecting a section (may have changed during async operation)
                if (isSelectingSection)
                {
                    return;
                }

                // Set selected section after sections are populated (on main thread to ensure UI is ready)
                await MainThread.InvokeOnMainThreadAsync(() => 
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        SetSelectedSection();
                    }
                });

                // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        ShowProgress = false;
                        // Allow screen to turn off after download completes
                        DeviceDisplay.Current.KeepScreenOn = false;
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow modal to continue functioning
                logger.Error(ex, "[MusicSectionSelection] RefreshFromState - Error during repopulation");
                // Allow screen to turn off after error
                MainThread.BeginInvokeOnMainThread(() => DeviceDisplay.Current.KeepScreenOn = false);
                // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        ShowProgress = false;
                    }
                });
            }
        }
        else
        {
            // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
            await MainThread.InvokeOnMainThreadAsync(() => 
            {
                if (!isDisposed && !isSelectingSection)
                {
                    ShowProgress = false;
                }
                if (!isDisposed)
                {
                    SetSelectedSection();
                }
            });
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }
        
        isDisposed = true;
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
                logger.Debug("[MusicSectionSelection] IsBusy changing from {OldValue} to {NewValue}. isSelectingSection={IsSelectingSection}, isInitializing={IsInitializing}. StackTrace: {StackTrace}",
                    isBusy, value, isSelectingSection, isInitializing, Environment.StackTrace);
            }
            SetProperty(ref isBusy, value);
        }
    }

    public bool ShowProgress
    {
        get => showProgress;
        set => SetProperty(ref showProgress, value);
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
