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
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicSectionSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    private string? lastPublicationCode;
    private MusicType? lastMusicType;
    private string? lastSectionCode;
    private bool isDisposed;
    private bool isSelectingSection;
    private bool isInitializing;
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = string.Empty;
    private MusicSectionSelectionStateChangeHandler? stateChangeHandler;

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }

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
        this.mapper = mapper;

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

            try
            {
                // Always use CurrentSchedule as the source of truth
                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule == null ||
                    !currentSchedule.MusicType.HasValue ||
                    string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    });
                    return;
                }

                var musicType = currentSchedule.MusicType.Value;
                var publicationCode = currentSchedule.MusicPublicationCode;
                var languageCode = currentSchedule.MusicLanguageCode; // May be null for instrumental music

                // Update progress
                ProgressPercent = 0.3;
                ProgressText = "Checking tracks...";

                // Get tracks for the selected section
                SortedDictionary<int, MusicTrack> tracks;
                if (musicType == MusicType.VocalMusic && !string.IsNullOrEmpty(languageCode))
                {
                    // For vocal music, use language code
                    tracks = await Task.Run(async () =>
                        await mediaService.GetVocalMusicTracks(languageCode, publicationCode));
                }
                else if (musicType == MusicType.Music)
                {
                    // For instrumental music, get tracks from the selected section
                    tracks = await Task.Run(async () =>
                        await mediaService.GetMelodyMusicTracksBySection(publicationCode, x.Section.SectionCode));
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    });
                    return;
                }

                if (tracks == null || tracks.Count == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    });
                    return;
                }

                // Update progress
                ProgressPercent = 0.7;
                ProgressText = "Completing...";

                // Use the first track from the selected section
                var firstTrack = tracks.Values.First();
                var trackNumber = firstTrack.Number;
                var trackTitle = firstTrack.Title;

                // Create MusicStateItem with selected section and track
                var musicStateItem = new MusicStateItem
                {
                    MusicType = musicType,
                    LanguageCode = languageCode,
                    PublicationCode = publicationCode,
                    SectionCode = x.Section.SectionCode,
                    TrackNumber = trackNumber,
                    Repeat = currentSchedule.MusicRepeat ?? false,
                    // Store display names
                    PublicationName = currentSchedule.MusicPublicationName,
                    SectionName = x.Name,
                    TrackName = trackTitle
                };

                // Dispatch MusicSectionSelectedAction to update CurrentSchedule
                dispatcher.Dispatch(new MusicSectionSelectedAction(musicStateItem));
                
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
                // The modal closing will naturally hide the busy overlay
                // IsBusy will be reset when the modal is disposed or when RefreshFromState is called next time
                await navigationService.PopModalAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[MusicSectionSelection] TrackSelectionCommand - Error selecting section");
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
            // Note: We intentionally do NOT set IsBusy = false in finally block
            // This keeps the busy overlay visible until the modal closes
            // IsBusy will be reset when RefreshFromState is called the next time the modal opens
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
            }));
        
        await PopulateSections(publicationCode, progressTracker);
    }

    /// <summary>
    /// Refreshes the sections list from the current state. Can be called when modal appears to ensure latest state is used.
    /// </summary>
    public async Task RefreshFromState()
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

                // Give CollectionView time to render before hiding busy indicator
                // This matches the pattern used in BiblePublicationTrackSelectionViewModel
                await Task.Delay(100);

                // Set IsBusy to false after collection is assigned and rendered - the busy overlay will hide instantly
                // BUT don't reset IsBusy if we're currently selecting a section (to avoid conflicts with TrackSelectionCommand)
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
                logger.Error(ex, "[MusicSectionSelection] RefreshFromState - Error during repopulation");
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
            // No repopulation needed - ensure IsBusy is false (in case it was left true from previous session)
            // BUT don't reset IsBusy if we're currently selecting a section (to avoid conflicts with TrackSelectionCommand)
            await MainThread.InvokeOnMainThreadAsync(() => 
            {
                if (!isDisposed && !isSelectingSection)
                {
                    IsBusy = false;
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
        // Show progress while fetching
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.1);
        
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (sectionViewModelList, selectedSection) = await Task.Run(async () =>
        {
            // For music publications (publications without language like "iam"), use empty string as language code
            // GetBiblePublicationSections will detect LanguageId == null and handle it appropriately
            // Pass progress parameter to support ad-hoc downloads (though music sections should be pre-harvested)
            var sectionsFromDb = await mediaService.GetBiblePublicationSections(string.Empty, publicationCode, progress);

            // If no sections found, sections might be being fetched (though music should be pre-harvested)
            // Add retry logic similar to Bible container for consistency
            if ((sectionsFromDb == null || sectionsFromDb.Count == 0))
            {
                logger.Information("[MusicSectionSelection] PopulateSections: No sections found initially for publication={PublicationCode}. " +
                    "Sections may be being fetched, will retry...",
                    publicationCode);
                
                // Retry up to 5 times with increasing delays to allow fetch to complete
                // Total wait time: 2s + 3s + 4s + 5s + 6s = 20 seconds
                for (int retry = 0; retry < 5; retry++)
                {
                    // Wait before retrying (2s, 3s, 4s, 5s, 6s)
                    progress?.UpdateProgress(0.2 + (retry / 5.0) * 0.3); // 0.2 to 0.5
                    await Task.Delay(1000 * (retry + 2));
                    
                    // Re-query to see if sections are now available
                    sectionsFromDb = await mediaService.GetBiblePublicationSections(string.Empty, publicationCode, progress);
                    
                    if (sectionsFromDb != null && sectionsFromDb.Count > 0)
                    {
                        logger.Information("[MusicSectionSelection] PopulateSections: Found {Count} sections on retry {Retry} for publication={PublicationCode}",
                            sectionsFromDb.Count, retry + 1, publicationCode);
                        break;
                    }
                }
            }

            progress?.UpdateProgress(0.7);

            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
                logger.Warning("[MusicSectionSelection] PopulateSections: No sections found for publication={PublicationCode}. " +
                    "This publication may not be harvested yet or may not have sections.",
                    publicationCode);
                progress?.UpdateProgress(1.0);
                progress?.SetIsVisible(false);
                return (new List<BiblePublicationSectionListViewItemModel>(), (BiblePublicationSectionListViewItemModel?)null);
            }

            var vms = new List<BiblePublicationSectionListViewItemModel>();
            BiblePublicationSectionListViewItemModel? selected = null;

            var currentSchedule = state.Value.CurrentSchedule;
            var currentSectionCode = currentSchedule?.MusicSectionCode;

            foreach (var section in sectionsFromDb.Values)
            {
                var sectionVm = new BiblePublicationSectionListViewItemModel(section);
                vms.Add(sectionVm);

                if (!string.IsNullOrEmpty(currentSectionCode) &&
                    section.SectionCode.Equals(currentSectionCode, StringComparison.OrdinalIgnoreCase))
                {
                    selected = sectionVm;
                    selected.IsSelected = true;
                }
            }

            // Sort using natural sort (numeric sections as int, non-numeric as string)
            vms.Sort();
            
            progress?.UpdateProgress(0.9);

            return (vms, selected);
        });
        
        progress?.UpdateProgress(1.0);
        progress?.SetIsVisible(false);

        // Minimal UI thread work - just swap the collection contents
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
