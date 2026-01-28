#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicPublicationSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Helper classes
    private readonly MusicPublicationSelectionStateManager stateManager;
    private readonly MusicPublicationSelectionDataProvider dataProvider;
    private readonly MusicPublicationSelectionCommandHandler commandHandler;
    private readonly MusicPublicationSelectionPropertyManager propertyManager;

    public MusicPublicationSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;

        // Initialize helper classes
        stateManager = new MusicPublicationSelectionStateManager();
        var biblePublicationService = serviceProvider.GetService<IBiblePublicationService>();
        var languageContentService = serviceProvider.GetService<ILanguageContentService>();
        dataProvider = new MusicPublicationSelectionDataProvider(mediaService, biblePublicationService, languageContentService, scopeFactory);
        commandHandler = new MusicPublicationSelectionCommandHandler(navigationService, state, dispatcher, scopeFactory, mediaService);
        propertyManager = new MusicPublicationSelectionPropertyManager();

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;

        // Check current state immediately in case state is already set
        // Use CurrentSchedule as source of truth
        var currentState = state.Value;
        if (currentState.CurrentSchedule != null && currentState.CurrentSchedule.MusicType.HasValue)
        {
            OnMusicInitialized(null, EventArgs.Empty);
        }

        TrackSelectionCommand = new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x != null)
            {
                await commandHandler.HandleTrackSelectionAsync(
                    x,
                    propertyManager.CurrentLanguage,
                    dataProvider,
                    stateManager.Current,
                    isVisible => propertyManager.ShowProgress = isVisible,
                    progress => propertyManager.ProgressPercent = progress,
                    text => propertyManager.ProgressText = text);
            }
        });

        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            propertyManager.IsBusy = true;

            try
            {
                // Ensure current is set from state if it's null
                stateManager.EnsureCurrentIsSet(state, mapper);

                // Ensure languages are populated before opening the modal
                await PopulateLanguages();

                // Wait a moment to ensure the collection is assigned and UI is ready
                await Task.Delay(50);

                // Double-check that languages are populated before opening modal
                if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
                {
                    await Task.Delay(100);
                    await PopulateLanguages();
                    await Task.Delay(50);
                }

                await navigationService.OpenLanguageModalAsync(this);
            }
            finally
            {
                propertyManager.IsBusy = false;
            }
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        SelectLanguageCommand = new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x != null)
            {
                await commandHandler.HandleLanguageSelectionAsync(
                    x,
                    dataProvider,
                    lang => propertyManager.CurrentLanguage = lang,
                    UpdateSelectedLanguage,
                    isVisible => propertyManager.ShowProgress = isVisible,
                    progress => propertyManager.ProgressPercent = progress,
                    text => propertyManager.ProgressText = text,
                    busy => propertyManager.IsBusy = busy);
            }
        });
    }

    private void OnMusicChanged(object? sender, EventArgs e)
    {
        stateManager.HandleMusicChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (langCode) => await PopulateSongPublications(langCode),
            SetSelectedSongPublication);
    }

    private void OnMusicInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleMusicInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            Initialize);
    }

    private void SetSelectedSongPublication()
    {
        dataProvider.SetSelectedSongPublication(
            stateManager.Current,
            dataProvider.SongPublicationVMsMapping,
            propertyManager.SelectedSongPublication,
            songPublication => propertyManager.SelectedSongPublication = songPublication);
    }

    public ICommand BackCommand { get; set; }
    public ICommand TrackSelectionCommand { get; set; }
    public ICommand OpenModalCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    /// <summary>
    /// Gets the FlowDirection based on the Music's selected language direction.
    /// Used for list items that display RTL content like song publication names.
    /// </summary>
    public FlowDirection ContentFlowDirection
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var direction = currentSchedule?.MusicLanguageDirection ?? "ltr";
            return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }
    }

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public ObservableCollection<PublicationListViewItemModel> SongPublications
    {
        get => propertyManager.SongPublications;
        set => propertyManager.SongPublications = value;
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => propertyManager.Languages;
        set => propertyManager.Languages = value;
    }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => propertyManager.CurrentLanguage;
        set => propertyManager.CurrentLanguage = value;
    }

    public string LanguageSearchTerm
    {
        get => propertyManager.LanguageSearchTerm;
        set => propertyManager.LanguageSearchTerm = value;
    }

    public PublicationListViewItemModel? SelectedSongPublication
    {
        get => propertyManager.SelectedSongPublication;
        set => propertyManager.SelectedSongPublication = value;
    }

    public object? SelectedItem => propertyManager.SelectedItem;

    public bool ShowProgress
    {
        get => propertyManager.ShowProgress;
        set => propertyManager.ShowProgress = value;
    }

    public double ProgressPercent
    {
        get => propertyManager.ProgressPercent;
        set => propertyManager.ProgressPercent = value;
    }

    public string ProgressText
    {
        get => propertyManager.ProgressText;
        set => propertyManager.ProgressText = value;
    }

    private async Task Initialize()
    {
        var current = stateManager.Current;
        if (current == null)
        {
            return;
        }

        // Handle instrumental music (no language needed)
        if (current.MusicType == MusicType.Music)
        {
            await PopulateSongPublications(null); // null language code for instrumental music
            return;
        }

        // Handle vocal music (requires language)
        var languageCode = current.LanguageCode;

        if (languageCode == null)
        {
            var languages = await mediaService.GetVocalMusicLanguages();
            // Default to English ("E") for Vocals, fallback to first available if English not present
            languageCode = languages.ContainsKey("E") ? "E" : languages.FirstOrDefault().Key;
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }
            current.LanguageCode = languageCode;
        }

        await PopulateLanguages();
        // When initializing, don't download all publications yet (only first publication in cascade)
        await PopulateSongPublications(languageCode, downloadAll: false);

        propertyManager.SetupLanguageSearchHandler(async (searchTerm) => await PopulateLanguages(searchTerm));
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        await dataProvider.PopulateLanguages(
            stateManager.Current,
            propertyManager.Languages,
            lang => propertyManager.CurrentLanguage = lang,
            searchTerm);
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures languages are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Wait for state to be updated (in case language was just changed)
        // This handles the race condition where the modal opens before state is fully updated
        // Use CurrentSchedule as primary source, but fall back to CurrentMusic if CurrentSchedule isn't updated yet
        const int maxWaitAttempts = 10;
        const int delayMs = 100;
        string? newLanguageCode = null;
        MusicType? musicType = null;

        for (int i = 0; i < maxWaitAttempts; i++)
        {
            var stateValue = state.Value;

            // Use CurrentSchedule as the source of truth
            if (stateValue.CurrentSchedule != null && stateValue.CurrentSchedule.MusicType.HasValue)
            {
                musicType = stateValue.CurrentSchedule.MusicType.Value;
                newLanguageCode = stateValue.CurrentSchedule.MusicLanguageCode;
                // For Vocals, we need language code; for Melodies, it can be null
                if (musicType == MusicType.VocalMusic && !string.IsNullOrEmpty(newLanguageCode))
                {
                    break;
                }
                else if (musicType == MusicType.Music)
                {
                    // For Melodies, language code can be null, so we can proceed
                    break;
                }
            }

            // Wait a bit and retry if language code is not set yet (for Vocals)
            await Task.Delay(delayMs);
        }

        var finalStateValue = state.Value;
        if (finalStateValue.CurrentSchedule == null || !musicType.HasValue)
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        // For Vocals, language code is required
        if (musicType.Value == MusicType.VocalMusic && string.IsNullOrEmpty(newLanguageCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
            return;
        }

        var current = stateManager.GetCurrentFromState(state, mapper);
        if (current != null)
        {
            stateManager.EnsureCurrentIsSet(state, mapper);
        }

        // Check if language code changed (need to repopulate song sections)
        var languageChanged = stateManager.LastLanguageCode != newLanguageCode;

        // For vocal music, ensure languages are populated
        if (musicType.Value == MusicType.VocalMusic)
        {
            if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
            {
                await PopulateLanguages();
            }

            // Ensure search handler is set up (in case it wasn't set up during initialization)
            // This is important when RefreshFromState is called before OnMusicInitialized
            propertyManager.SetupLanguageSearchHandler(async (searchTerm) => await PopulateLanguages(searchTerm));
        }

        // For Vocals, if no language is selected but languages are available, select based on current schedule
        string? languageCodeToUse = null;
        if (finalStateValue.CurrentSchedule?.MusicType == MusicType.VocalMusic &&
            propertyManager.CurrentLanguage == null &&
            propertyManager.Languages != null &&
            propertyManager.Languages.Count > 0)
        {
            // Try to select the language from current schedule state
            var scheduleLanguageCode = finalStateValue.CurrentSchedule.MusicLanguageCode ?? newLanguageCode;
            LanguageListViewItemModel? languageToSelect = null;

            if (!string.IsNullOrEmpty(scheduleLanguageCode))
            {
                languageToSelect = propertyManager.Languages.FirstOrDefault(l => l.Code == scheduleLanguageCode);
            }

            // If no language from schedule, default to English
            if (languageToSelect == null)
            {
                languageToSelect = propertyManager.Languages.FirstOrDefault(l => l.Code == "E")
                    ?? propertyManager.Languages.FirstOrDefault();
            }

            if (languageToSelect != null)
            {
                propertyManager.CurrentLanguage = languageToSelect;
                languageToSelect.IsSelected = true;
                languageCodeToUse = languageToSelect.Code;
            }
        }
        else if (propertyManager.CurrentLanguage != null)
        {
            languageCodeToUse = propertyManager.CurrentLanguage.Code;
        }
        else if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
        {
            languageCodeToUse = current.LanguageCode;
        }
        else if (!string.IsNullOrEmpty(newLanguageCode))
        {
            languageCodeToUse = newLanguageCode;
        }

        // Create progress tracker for modal open (only when fetching)
        bool needsFetch = false;
        if (musicType.Value == MusicType.Music)
        {
            needsFetch = propertyManager.SongPublications == null || propertyManager.SongPublications.Count == 0;
        }
        else if (!string.IsNullOrEmpty(languageCodeToUse))
        {
            needsFetch = propertyManager.SongPublications == null || propertyManager.SongPublications.Count == 0 || languageChanged;
        }
        
        IFetchProgress? progressTracker = null;
        if (needsFetch)
        {
            progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                progress => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ProgressPercent = progress),
                text => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ProgressText = text),
                isVisible => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ShowProgress = isVisible));
        }

        // For instrumental music, populate publications directly (no language needed)
        if (musicType.Value == MusicType.Music)
        {
            if (propertyManager.SongPublications == null || propertyManager.SongPublications.Count == 0)
            {
                await PopulateSongPublications(null, downloadAll: true, progressTracker); // null language code for instrumental music
            }
            else
            {
                // Publications already populated, just set selected
                SetSelectedSongPublication();
            }
        }
        // For vocal music, always repopulate song sections if:
        // 1. We have a language code
        // 2. Song sections aren't already populated
        // 3. Language code changed (cascade effect)
        else if (!string.IsNullOrEmpty(languageCodeToUse) &&
            (propertyManager.SongPublications == null || propertyManager.SongPublications.Count == 0 || languageChanged))
        {
            // When RefreshFromState is called (publication modal opens), download all publications
            // When language changes (cascade), don't download all yet (only first publication in cascade)
            bool isModalOpening = propertyManager.SongPublications == null || propertyManager.SongPublications.Count == 0;
            await PopulateSongPublications(languageCodeToUse, downloadAll: isModalOpening, progressTracker);
        }
        else if (!string.IsNullOrEmpty(languageCodeToUse))
        {
            // Song sections already populated, just set selected
            SetSelectedSongPublication();
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            propertyManager.IsBusy = false;
            if (progressTracker != null)
            {
                propertyManager.ShowProgress = false;
            }
        });
    }

    private async Task PopulateSongPublications(string? languageCode, bool downloadAll = false, IFetchProgress? progress = null)
    {
        await dataProvider.PopulateSongPublications(
            languageCode,
            stateManager.Current,
            propertyManager.SongPublications,
            songPublication => propertyManager.SelectedSongPublication = songPublication,
            downloadAll,
            progress);
    }

    private void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        if (propertyManager.CurrentLanguage != null)
        {
            propertyManager.CurrentLanguage.IsSelected = false;
        }

        propertyManager.CurrentLanguage = language;
        propertyManager.CurrentLanguage!.IsSelected = true;
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        state.StateChanged -= OnMusicChanged;
        propertyManager.RemoveLanguageSearchHandler();
    }
}
