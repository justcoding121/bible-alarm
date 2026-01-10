#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.ViewModels.Bible.SectionSelectionViewModelHelpers;

namespace Bible.Alarm.ViewModels.Bible;

public sealed class SectionSelectionViewModel : ObservableObject, IDisposable
{
    private BibleReadingSchedule? current;

    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    private BibleReadingSchedule? lastCurrent;
    private string? lastLanguageCode;
    private string? lastPublicationCode;

    // Helper class
    private readonly StateChangeHandler stateChangeHandler;

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand ChapterSelectionCommand { get; set; }

    public SectionSelectionViewModel(ILogger logger, IMediaService mediaService, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Don't initialize here - let OnBibleReadingInitialized handle it
        // This ensures we always get the latest state when the modal opens
        // But we need to initialize tracking variables to null so OnBibleReadingInitialized can detect changes

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        ChapterSelectionCommand = new AsyncRelayCommand<BibleSectionListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            // Always use CurrentSchedule as the source of truth for language/publication codes
            // This ensures we use the latest state, not stale data from 'current' field
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null ||
                string.IsNullOrEmpty(currentSchedule.BibleReadingLanguageCode) ||
                string.IsNullOrEmpty(currentSchedule.BibleReadingPublicationCode))
            {
                return;
            }

            // Check if the selected section is the same as the current section
            var currentSectionNumber = currentSchedule.BibleReadingSectionNumber;
            var isSameSection = currentSectionNumber.HasValue && currentSectionNumber.Value == x.Number;

            // Get chapters for the selected section using the latest language/publication from CurrentSchedule
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(currentSchedule.BibleReadingLanguageCode, currentSchedule.BibleReadingPublicationCode, x.Number));

            if (chapters == null || chapters.Count == 0)
            {
                return;
            }

            // If it's the same section, preserve the current chapter number (if valid)
            // Otherwise, use the first chapter
            int chapterNumber;
            if (isSameSection && currentSchedule.BibleReadingChapterNumber.HasValue)
            {
                var currentChapterNumber = currentSchedule.BibleReadingChapterNumber.Value;
                // Verify the current chapter exists in the chapters list
                if (chapters.ContainsKey(currentChapterNumber))
                {
                    chapterNumber = currentChapterNumber;
                }
                else
                {
                    // Current chapter doesn't exist in this section, use first chapter
                    chapterNumber = chapters.Values.First().Number;
                }
            }
            else
            {
                // Different section selected, use first chapter
                chapterNumber = chapters.Values.First().Number;
            }

            // Create BibleReadingStateItem with selected section and chapter
            // IMPORTANT: Include display names from list items (no database query needed)
            // Get language/publication codes and display names from CurrentSchedule (they should already be populated)
            var bibleReadingItem = new BibleReadingStateItem
            {
                LanguageCode = currentSchedule.BibleReadingLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode,
                SectionNumber = x.Number,
                ChapterNumber = chapterNumber,
                // Store display names from list items and current state
                LanguageName = currentSchedule.BibleReadingLanguageName,
                PublicationName = currentSchedule.BibleReadingPublicationName,
                SectionName = x.Name
            };

            // Dispatch ChapterSelectedAction to update CurrentBibleReadingSchedule
            // Effect will automatically sync to CurrentSchedule
            dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));

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

        // Only subscribe OnBibleReadingChanged to state changes
        // OnBibleReadingInitialized will only be called once manually in the constructor
        state.StateChanged += OnBibleReadingChanged;

        // Always trigger initialization immediately to ensure we read the latest state
        // This is especially important when the modal opens after a language change
        // This should only run once, not on every state change
        OnBibleReadingInitialized(null, EventArgs.Empty);
    }

    private void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        stateChangeHandler.HandleStateChanged(state.Value);
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        // This method should only be called once during construction
        // Subsequent state changes should be handled by OnBibleReadingChanged
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

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;

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

        // Update current if we have CurrentBibleReadingSchedule (for other properties like SectionNumber)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionNumber = currentSchedule.BibleReadingSectionNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
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
                // This matches the pattern used in ChapterSelectionViewModel
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
        state.StateChanged -= OnBibleReadingChanged;
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
        // This matches the pattern used in ChapterSelectionViewModel
        var section = Sections.FirstOrDefault(b => b.Number == current.SectionNumber);
        if (section != null)
        {
            SelectedSection = section;
            SelectedSection.IsSelected = true;
        }
    }

    public BibleSectionListViewItemModel? SelectedSection { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<BibleSectionListViewItemModel> sections = [];

    public ObservableCollection<BibleSectionListViewItemModel> Sections
    {
        get => sections;
        set => SetProperty(ref sections, value);
    }

    private async Task Initialize(string languageCode, string publicationCode) => await PopulateSections(languageCode, publicationCode);

    private readonly Dictionary<int, BibleSectionListViewItemModel> sectionVMsMapping = [];

    private async Task PopulateSections(string languageCode, string publicationCode)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (sectionViewModelList, newMapping, selectedSection) = await Task.Run(async () =>
        {
            var sectionsFromDb = await mediaService.GetBibleSections(languageCode, publicationCode);
            
            if (sectionsFromDb == null)
            {
                return (new List<BibleSectionListViewItemModel>(), new Dictionary<int, BibleSectionListViewItemModel>(), (BibleSectionListViewItemModel?)null);
            }

            var vms = new List<BibleSectionListViewItemModel>();
            var mapping = new Dictionary<int, BibleSectionListViewItemModel>();
            BibleSectionListViewItemModel? selected = null;

            foreach (var section in sectionsFromDb.Values)
            {
                var sectionVm = new BibleSectionListViewItemModel(section);
                vms.Add(sectionVm);
                mapping[sectionVm.Number] = sectionVm;

                if (current != null && current.SectionNumber == section.Number)
                {
                    selected = sectionVm;
                    selected.IsSelected = true;
                }
            }

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

public sealed class BibleSectionListViewItemModel(BibleSection section) : ObservableObject, IComparable
{
    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string Name => section.Name;
    public int Number => section.Number;

    public int CompareTo(object? obj) => obj is not BibleSectionListViewItemModel other ? 1 : Number.CompareTo(other.Number);
}
