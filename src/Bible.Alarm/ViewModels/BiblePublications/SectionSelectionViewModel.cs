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
using Bible.Alarm.ViewModels.BiblePublications.SectionSelectionViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class SectionSelectionViewModel : ObservableObject, IDisposable
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

    // Helper class
    private readonly StateChangeHandler stateChangeHandler;

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }

    public SectionSelectionViewModel(ILogger logger, IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

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
            try
            {
                if (x == null)
                {
                    return;
                }

                // Always use CurrentSchedule as the source of truth for language/publication codes
                // This ensures we use the latest state, not stale data from 'current' field
                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule == null || string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
                {
                    logger.Warning("SectionSelectionViewModel: TrackSelectionCommand - CurrentSchedule is null or PublicationCode is empty");
                    return;
                }
                
                // Validate section number - section 0 is invalid (sections should start from 1)
                if (x.Number <= 0)
                {
                    logger.Warning("SectionSelectionViewModel: TrackSelectionCommand - Invalid section number {SectionNumber} for publication={PublicationCode}",
                        x.Number, currentSchedule.BiblePublicationCode);
                    return;
                }
                
                // Language code can be null/empty for publications without language (e.g., "iam")
                // GetBiblePublicationTracks handles this case
                var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;

                // Check if the selected section is the same as the current section
                var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber;
                var isSameSection = currentSectionNumber.HasValue && currentSectionNumber.Value == x.Number;

                // Get tracks for the selected section using the latest language/publication from CurrentSchedule
                // Language code can be null/empty for publications without language - GetBiblePublicationTracks handles this
                var tracks = await Task.Run(async () =>
                    await mediaService.GetBiblePublicationTracks(languageCode, currentSchedule.BiblePublicationCode, x.Number));

                // If no tracks found, check if section exists and harvest tracks if needed
                if ((tracks == null || tracks.Count == 0) && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Information("SectionSelectionViewModel: No tracks found for section={SectionNumber}, publication={PublicationCode}, language={LanguageCode}. Checking if section exists and harvesting tracks if needed...",
                        x.Number, currentSchedule.BiblePublicationCode, languageCode);
                    
                    // Get section code from the section item
                    var sectionCode = x.Section.SectionCode;
                    
                    // Check if section exists and harvest tracks if needed
                    var languageContentService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>();
                    if (languageContentService != null)
                    {
                        var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                            currentSchedule.BiblePublicationCode, sectionCode, languageCode);
                        
                        if (fetchSuccess)
                        {
                            // Re-query tracks after harvesting
                            tracks = await Task.Run(async () =>
                                await mediaService.GetBiblePublicationTracks(languageCode, currentSchedule.BiblePublicationCode, x.Number));
                        }
                    }
                }

                if (tracks == null || tracks.Count == 0)
                {
                    logger.Warning("SectionSelectionViewModel: TrackSelectionCommand - No tracks found for section={SectionNumber}, publication={PublicationCode}, language={LanguageCode}",
                        x.Number, currentSchedule.BiblePublicationCode, languageCode ?? "(null)");
                    return;
                }

                // If it's the same section, preserve the current track number (if valid)
                // Otherwise, use the first track
                int trackNumber;
                string? trackTitle = null;
                if (isSameSection && currentSchedule.BiblePublicationTrackNumber.HasValue)
                {
                    var currentTrackNumber = currentSchedule.BiblePublicationTrackNumber.Value;
                    // Verify the current track exists in the tracks list
                    if (tracks.TryGetValue(currentTrackNumber, out var existingTrack))
                    {
                        trackNumber = currentTrackNumber;
                        trackTitle = existingTrack.Title;
                    }
                    else
                    {
                        // Current track doesn't exist in this section, use first track
                        var firstTrack = tracks.Values.First();
                        trackNumber = firstTrack.Number;
                        trackTitle = firstTrack.Title;
                    }
                }
                else
                {
                    // Different section selected, use first track
                    var firstTrack = tracks.Values.First();
                    trackNumber = firstTrack.Number;
                    trackTitle = firstTrack.Title;
                }

                // Create BiblePublicationStateItem with selected section and track
                // IMPORTANT: Include display names from list items (no database query needed)
                // Get language/publication codes and display names from CurrentSchedule (they should already be populated)
                // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
                var biblePublicationItem = new BiblePublicationStateItem
                {
                    CategoryId = currentSchedule.BiblePublicationCategoryId,
                    CategoryName = currentSchedule.BiblePublicationCategoryName,
                    LanguageCode = languageCode, // Can be empty for publications without language
                    PublicationCode = currentSchedule.BiblePublicationCode,
                    SectionNumber = x.Number,
                    TrackNumber = trackNumber,
                    // Store display names from list items and current state
                    LanguageName = currentSchedule.BiblePublicationLanguageName,
                    LanguageDirection = currentSchedule.BiblePublicationLanguageDirection,
                    PublicationName = currentSchedule.BiblePublicationName,
                    SectionName = x.Name,
                    TrackTitle = trackTitle
                };

                // Dispatch TrackSelectedAction to update CurrentBiblePublicationSchedule
                // Effect will automatically sync to CurrentSchedule
                dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));

                // Navigate back to schedule page
                await navigationService.PopModalAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "SectionSelectionViewModel: TrackSelectionCommand - Error selecting section");
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
        if (needsRepopulation && !isDisposed)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => 
                {
                    if (!isDisposed)
                    {
                        IsBusy = true;
                    }
                });
                
                if (isDisposed)
                {
                    return;
                }
                
                // Create progress tracker for modal open
                var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                    progress => MainThread.BeginInvokeOnMainThread(() => 
                    {
                        if (!isDisposed)
                        {
                            ProgressPercent = progress;
                        }
                    }),
                    text => MainThread.BeginInvokeOnMainThread(() => 
                    {
                        if (!isDisposed)
                        {
                            ProgressText = text;
                        }
                    }),
                    isVisible => MainThread.BeginInvokeOnMainThread(() => 
                    {
                        if (!isDisposed)
                        {
                            ShowProgress = isVisible;
                        }
                    }));
                
                // Use the latest state values, not cached ones
                await Initialize(newLanguageCode, newPublicationCode, progressTracker);

                // Set selected section after sections are populated (on main thread to ensure UI is ready)
                await MainThread.InvokeOnMainThreadAsync(() => SetSelectedSection());

                // Give CollectionView time to render before hiding busy indicator
                // This matches the pattern used in TrackSelectionViewModel
                await Task.Delay(100);

                // Set IsBusy to false after collection is assigned and rendered - the busy overlay will hide instantly
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!isDisposed)
                    {
                        IsBusy = false;
                        ShowProgress = false;
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow modal to continue functioning
                logger.Error(ex, "SectionSelectionViewModel: RefreshFromState - Error during repopulation");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!isDisposed)
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
            // Ensure this runs on main thread for UI updates
            MainThread.BeginInvokeOnMainThread(() => 
            {
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

        // Find the section from the Sections collection (same instance as in ItemsSource)
        // This matches the pattern used in TrackSelectionViewModel
        // Compare SectionCode (string) with Number (int) by converting Number to string or parsing SectionCode
        // Handle both numeric codes (e.g., "1") and non-numeric codes (e.g., "iam-1")
        var section = Sections.FirstOrDefault(b => 
            !string.IsNullOrEmpty(current.SectionCode) && 
            (b.Number.ToString() == current.SectionCode || 
             (int.TryParse(current.SectionCode, out var num) && num == b.Number) ||
             // Handle non-numeric codes like "iam-1" by extracting number part
             (current.SectionCode.Contains('-') && 
              current.SectionCode.Split('-').Length > 1 && 
              int.TryParse(current.SectionCode.Split('-')[^1], out var extractedNum) && 
              extractedNum == b.Number)));
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
        // Show progress while fetching
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.1);
        
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (sectionViewModelList, newMapping, selectedSection) = await Task.Run(async () =>
        {
            var sectionsFromDb = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);

            // If no sections found and this is a non-English language, sections might be being fetched
            // Retry a few times with delays to allow the fetch to complete
            if ((sectionsFromDb == null || sectionsFromDb.Count == 0) && 
                !string.IsNullOrEmpty(languageCode) && 
                !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                logger.Information("PopulateSections: No sections found initially for publication={PublicationCode}, language={LanguageCode}. " +
                    "Sections may be being fetched, will retry...",
                    publicationCode, languageCode);
                
                // Retry up to 5 times with increasing delays to allow fetch to complete
                // Total wait time: 2s + 3s + 4s + 5s + 6s = 20 seconds
                for (int retry = 0; retry < 5; retry++)
                {
                    // Wait before retrying (2s, 3s, 4s, 5s, 6s)
                    progress?.UpdateProgress(0.2 + (retry / 5.0) * 0.3); // 0.2 to 0.5
                    await Task.Delay(1000 * (retry + 2));
                    
                    // Re-query to see if sections are now available
                    sectionsFromDb = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);
                    
                    if (sectionsFromDb != null && sectionsFromDb.Count > 0)
                    {
                        logger.Information("PopulateSections: Found {Count} sections on retry {Retry} for publication={PublicationCode}, language={LanguageCode}",
                            sectionsFromDb.Count, retry + 1, publicationCode, languageCode);
                        break;
                    }
                }
            }

            progress?.UpdateProgress(0.7);
            
            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
                logger.Warning("PopulateSections: No sections found for publication={PublicationCode}, language={LanguageCode}. " +
                    "This publication may not be harvested yet or may not have sections.",
                    publicationCode, languageCode ?? "(null)");
                progress?.UpdateProgress(1.0);
                progress?.SetIsVisible(false);
                return (new List<BiblePublicationSectionListViewItemModel>(), new Dictionary<int, BiblePublicationSectionListViewItemModel>(), (BiblePublicationSectionListViewItemModel?)null);
            }

            var vms = new List<BiblePublicationSectionListViewItemModel>();
            var mapping = new Dictionary<int, BiblePublicationSectionListViewItemModel>();
            BiblePublicationSectionListViewItemModel? selected = null;

            foreach (var section in sectionsFromDb.Values)
            {
                var sectionVm = new BiblePublicationSectionListViewItemModel(section);
                vms.Add(sectionVm);
                mapping[sectionVm.Number] = sectionVm;

                if (current != null && current.SectionCode == section.SectionCode)
                {
                    selected = sectionVm;
                    selected.IsSelected = true;
                }
            }

            // Sort using natural sort (numeric sections as int, non-numeric as string)
            vms.Sort();
            
            progress?.UpdateProgress(0.9);

            return (vms, mapping, selected);
        });
        
        progress?.UpdateProgress(1.0);
        progress?.SetIsVisible(false);

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

            if (selectedSection is not null)
            {
                SelectedSection = selectedSection;
            }
        });
    }
}

public sealed class BiblePublicationSectionListViewItemModel(BiblePublicationSection section) : ObservableObject, IComparable
{
    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    /// <summary>
    /// Exposes the underlying section for comparison.
    /// </summary>
    public BiblePublicationSection Section => section;

    /// <summary>
    /// Gets the section name with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Name => System.Net.WebUtility.HtmlDecode(section.Name).Replace('\u00A0', ' ');
    /// <summary>
    /// Gets the section number for sorting/comparison.
    /// Tries to parse SectionCode as int.
    /// For non-numeric codes like "iam-1", extracts the number part.
    /// Returns 0 if SectionCode cannot be parsed or number cannot be extracted.
    /// </summary>
    public int Number
    {
        get
        {
            // Try to parse SectionCode as int (for numeric codes like "1", "2")
            if (int.TryParse(section.SectionCode, out var num))
            {
                return num;
            }
            
            // For non-numeric codes like "iam-1", extract the number part
            // e.g., "iam-1" -> 1, "iam-2" -> 2
            var parts = section.SectionCode.Split('-');
            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out var extractedNumber))
            {
                return extractedNumber;
            }
            
            return 0;
        }
    }

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationSectionListViewItemModel other)
        {
            return 1;
        }
        
        // Natural sort: if SectionCode is numeric, sort as int; otherwise sort as string
        var thisIsNumeric = int.TryParse(section.SectionCode, out var thisNum);
        var otherIsNumeric = int.TryParse(other.Section.SectionCode, out var otherNum);
        
        if (thisIsNumeric && otherIsNumeric)
        {
            // Both are numeric - compare as integers
            return thisNum.CompareTo(otherNum);
        }
        
        if (thisIsNumeric && !otherIsNumeric)
        {
            // This is numeric, other is not - numeric comes first
            return -1;
        }
        
        if (!thisIsNumeric && otherIsNumeric)
        {
            // This is not numeric, other is - numeric comes first
            return 1;
        }
        
        // Both are non-numeric - compare as strings
        return string.Compare(section.SectionCode, other.Section.SectionCode, StringComparison.OrdinalIgnoreCase);
    }
}
