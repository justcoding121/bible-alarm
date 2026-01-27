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
    private bool showProgress = false;
    private double progressPercent = 0.0;
    private string progressText = string.Empty;

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
                await navigationService.PopModalAsync();
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    ShowProgress = false;
                });
            }
        });

        state.StateChanged += OnStateChanged;

        // Initialize immediately
        _ = RefreshFromState();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        _ = RefreshFromState();
    }

    /// <summary>
    /// Refreshes the sections list from the current state.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null ||
            !stateValue.CurrentSchedule.MusicType.HasValue ||
            string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var musicType = currentSchedule.MusicType.Value;
        var publicationCode = currentSchedule.MusicPublicationCode;

        // Only handle instrumental music (Music) for now
        // Vocal music sections use the Bible publication section selection
        if (musicType != MusicType.Music)
        {
            return;
        }

        // Check if publication code changed (need to repopulate sections)
        var publicationCodeChanged = lastPublicationCode != publicationCode;
        var musicTypeChanged = lastMusicType != musicType;
        var needsRepopulation = publicationCodeChanged || musicTypeChanged || !initComplete;

        // Update tracking variables
        lastPublicationCode = publicationCode;
        lastMusicType = musicType;

        if (!initComplete)
        {
            initComplete = true;
        }

        // Initialize or repopulate with the current publication
        if (needsRepopulation)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                
                // Create progress tracker for modal open
                var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                    progress => _ = MainThread.InvokeOnMainThreadAsync(() => ProgressPercent = progress),
                    text => _ = MainThread.InvokeOnMainThreadAsync(() => ProgressText = text),
                    isVisible => _ = MainThread.InvokeOnMainThreadAsync(() => ShowProgress = isVisible));
                
                await PopulateSections(publicationCode, progressTracker);

                // Set selected section after sections are populated
                await MainThread.InvokeOnMainThreadAsync(() => SetSelectedSection());

                // Give CollectionView time to render before hiding busy indicator
                await Task.Delay(100);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    ShowProgress = false;
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "MusicSectionSelectionViewModel: RefreshFromState - Error during repopulation");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    ShowProgress = false;
                });
            }
        }
        else
        {
            // Update selected section when state changes
            MainThread.BeginInvokeOnMainThread(() => SetSelectedSection());
        }
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
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
            var sectionsFromDb = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
            
            progress?.UpdateProgress(0.7);

            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
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
