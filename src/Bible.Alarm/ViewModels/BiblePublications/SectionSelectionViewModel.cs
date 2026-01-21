#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
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
            if (x == null)
            {
                return;
            }

            // Always use CurrentSchedule as the source of truth for language/publication codes
            // This ensures we use the latest state, not stale data from 'current' field
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode) ||
                string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
            {
                return;
            }

            // Check if the selected section is the same as the current section
            var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber;
            var isSameSection = currentSectionNumber.HasValue && currentSectionNumber.Value == x.Number;

            // Get tracks for the selected section using the latest language/publication from CurrentSchedule
            var tracks = await Task.Run(async () =>
                await mediaService.GetBiblePublicationTracks(currentSchedule.BiblePublicationLanguageCode, currentSchedule.BiblePublicationCode, x.Number));

            if (tracks == null || tracks.Count == 0)
            {
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
            var biblePublicationItem = new BiblePublicationStateItem
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
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
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationCode;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
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
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionNumber.HasValue ? currentSchedule.BiblePublicationSectionNumber.Value.ToString() : null,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionCode = (currentSchedule?.BiblePublicationSectionNumber ?? 1).ToString(),
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
            lastCurrent = current;
        }

        if (!initComplete)
        {
            initComplete = true;
        }

        // Initialize or repopulate with the current language/publication
        if (needsRepopulation)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                // Use the latest state values, not cached ones
                await Initialize(newLanguageCode, newPublicationCode);

                // Set selected section after sections are populated (on main thread to ensure UI is ready)
                await MainThread.InvokeOnMainThreadAsync(() => SetSelectedSection());

                // Give CollectionView time to render before hiding busy indicator
                // This matches the pattern used in TrackSelectionViewModel
                await Task.Delay(100);

                // Set IsBusy to false after collection is assigned and rendered - the busy overlay will hide instantly
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow modal to continue functioning
                logger.Error(ex, "SectionSelectionViewModel: RefreshFromState - Error during repopulation");
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
        }
        else
        {
            // Update selected section when state changes (e.g., after navigating back)
            // Ensure this runs on main thread for UI updates
            MainThread.BeginInvokeOnMainThread(() => SetSelectedSection());
        }
    }

    public void Dispose()
    {
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
        var section = Sections.FirstOrDefault(b => 
            !string.IsNullOrEmpty(current.SectionCode) && 
            (b.Number.ToString() == current.SectionCode || (int.TryParse(current.SectionCode, out var num) && num == b.Number)));
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

    private async Task Initialize(string languageCode, string publicationCode) => await PopulateSections(languageCode, publicationCode);

    private readonly Dictionary<int, BiblePublicationSectionListViewItemModel> sectionVMsMapping = [];

    private async Task PopulateSections(string languageCode, string publicationCode)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (sectionViewModelList, newMapping, selectedSection) = await Task.Run(async () =>
        {
            var sectionsFromDb = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);

            if (sectionsFromDb == null)
            {
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

            return (vms, mapping, selected);
        });

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
    /// Tries to parse SectionCode as int, or uses BookNum if available.
    /// Returns 0 if neither can be determined.
    /// </summary>
    public int Number
    {
        get
        {
            if (int.TryParse(section.SectionCode, out var num))
            {
                return num;
            }
            return section.BookNum ?? 0;
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
