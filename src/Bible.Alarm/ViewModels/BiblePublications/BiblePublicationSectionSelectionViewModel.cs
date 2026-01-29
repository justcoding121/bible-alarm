#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationSectionSelectionViewModel : ObservableObject, IDisposable
{
    private BiblePublicationSchedule? current;

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    private BiblePublicationSchedule? lastCurrent;
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = string.Empty;
    private bool isDisposed = false;
    private bool isSelectingSection;

    // Helper class
    private readonly StateChangeHandler stateChangeHandler;
    private readonly SectionListLoader sectionListLoader;
    private readonly TrackSelectionResolver trackSelectionResolver;

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }

    public BiblePublicationSectionSelectionViewModel(ILogger logger, IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        sectionListLoader = new SectionListLoader(logger, mediaService);
        trackSelectionResolver = new TrackSelectionResolver(logger, mediaService);

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
                // Always use CurrentSchedule as the source of truth for language/publication codes
                // This ensures we use the latest state, not stale data from 'current' field
                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule == null || string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
                {
                    logger.Warning("BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - CurrentSchedule is null or PublicationCode is empty");
                    return;
                }
                
                // Validate section number - section 0 is invalid (sections should start from 1)
                if (x.Number <= 0)
                {
                    logger.Warning("BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - Invalid section number {SectionNumber} for publication={PublicationCode}",
                        x.Number, currentSchedule.BiblePublicationCode);
                    return;
                }
                
                // Track start time to ensure minimum display duration
                var startTime = DateTime.UtcNow;
                const int minimumDisplayMs = 800; // Minimum time to show progress indicator

                // Show progress immediately on UI thread before any async work
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = true;
                    ShowProgress = true;
                    ProgressPercent = 0.0;
                    ProgressText = "Loading...";
                });
                
                // Give UI thread enough time to render the progress indicator
                await Task.Delay(300);
                
                // Language code can be null/empty for publications without language (e.g., "iam")
                // GetBiblePublicationTracks handles this case
                var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;

                // Check if the selected section is the same as the current section
                var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber;
                var isSameSection = currentSectionNumber.HasValue && currentSectionNumber.Value == x.Number;

                // Update progress
                ProgressPercent = 0.3;
                ProgressText = "Checking tracks...";

                var biblePublicationItem = await trackSelectionResolver.BuildSelectionAsync(
                    x,
                    currentSchedule,
                    () => ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>());

                if (biblePublicationItem == null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    });
                    return;
                }

                // Update progress
                ProgressPercent = 0.9;
                ProgressText = "Completing...";

                // Dispatch TrackSelectedAction to update CurrentBiblePublicationSchedule
                // Effect will automatically sync to CurrentSchedule
                dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
                
                // Ensure minimum display time has elapsed
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (elapsed < minimumDisplayMs)
                {
                    var remaining = minimumDisplayMs - (int)elapsed;
                    await Task.Delay(remaining);
                }
                else
                {
                    await Task.Delay(200); // Brief delay to show completion
                }
                
                ProgressPercent = 1.0;
                ProgressText = "100%";
                await Task.Delay(100);
                
                // Navigate back to schedule page
                // Keep IsBusy = true until modal closes - don't hide busy overlay here
                await navigationService.PopModalAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - Error selecting section");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    ShowProgress = false;
                });
            }
            finally
            {
                // Reset flag after operation completes (whether success or error)
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
            (lang, pub) => _ = Initialize(lang, pub),
            SetSelectedSection);

        // Only subscribe OnBiblePublicationChanged to state changes
        // OnBiblePublicationInitialized will only be called once manually in the constructor
        state.StateChanged += OnBiblePublicationChanged;

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

    private void OnBiblePublicationInitialized(object? o, EventArgs eventArgs)
    {
        // This method should only be called once during construction
        // Subsequent state changes should be handled by OnBiblePublicationChanged
        if (initComplete)
        {
            return;
        }

        // Always read the latest state when initializing
        // Fire-and-forget: initialization happens asynchronously, errors are handled within RefreshFromState
        _ = RefreshFromState();
    }

    /// <summary>
    /// Refreshes the sections list from the current state. Can be called when modal appears to ensure latest state is used.
    /// </summary>
    public async Task RefreshFromState()
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
            SectionCode = currentSchedule.BiblePublicationSectionNumber.HasValue ? currentSchedule.BiblePublicationSectionNumber.Value.ToString() : null,
            TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };
        lastCurrent = current;

        if (!initComplete)
        {
            initComplete = true;
        }

        // Initialize or repopulate with the current language/publication
        if (needsRepopulation && !isDisposed && !isSelectingSection)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => 
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        IsBusy = true;
                    }
                });
                
                if (isDisposed || isSelectingSection)
                {
                    return;
                }
                
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
                    }));
                
                // Use the latest state values, not cached ones
                await Initialize(newLanguageCode, newPublicationCode, progressTracker);

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

                // Give CollectionView time to render before hiding busy indicator
                // This matches the pattern used in BiblePublicationTrackSelectionViewModel
                await Task.Delay(100);

                // Set IsBusy to false after collection is assigned and rendered
                // Don't reset IsBusy if we're currently selecting a section (to avoid conflicts with TrackSelectionCommand)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow modal to continue functioning
                logger.Error(ex, "BiblePublicationSectionSelectionViewModel: RefreshFromState - Error during repopulation");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!isDisposed && !isSelectingSection)
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    }
                });
            }
        }
        else
        {
            // Update selected section when state changes (e.g., after navigating back)
            // Don't reset IsBusy or update selection if we're currently selecting a section
            MainThread.BeginInvokeOnMainThread(() => 
            {
                if (!isDisposed && !isSelectingSection)
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
        state.StateChanged -= OnBiblePublicationChanged;
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

    public BiblePublicationSectionListViewItemModel? SelectedSection { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
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

    private async Task Initialize(string languageCode, string publicationCode, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null) => await PopulateSections(languageCode, publicationCode, progress);

    private readonly Dictionary<int, BiblePublicationSectionListViewItemModel> sectionVMsMapping = [];

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
