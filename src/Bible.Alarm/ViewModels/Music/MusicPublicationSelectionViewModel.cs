#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
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

public sealed class MusicPublicationSelectionViewModel : ObservableObject, IListViewModel, IHasFetchErrorListViewModel, IDisposable
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
    private PropertyChangedEventHandler? propertyManagerPropertyChangedHandler;

    // Cancellation support for fetch operations
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState/Initialize calls - ensures concurrent calls wait for each other
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

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
        commandHandler = new MusicPublicationSelectionCommandHandler(navigationService, state, dispatcher, mediaService);
        propertyManager = new MusicPublicationSelectionPropertyManager();
        SetupPropertyManagerForwarding();

        state.StateChanged += OnMusicInitialized;
        state.StateChanged += OnMusicChanged;

        // Check current state immediately in case state is already set
        // Use CurrentSchedule as source of truth
        var currentState = state.Value;
        if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.MusicPublicationCode))
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

        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);
        RetryFetchCommand = new AsyncRelayCommand(RetryFetchAsync);
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
    public ICommand CancelFetchCommand { get; }
    public ICommand RetryFetchCommand { get; }

    private async Task CancelFetchAsync()
    {
        Serilog.Log.Information("MusicPublicationSelectionViewModel: CancelFetchCommand - User cancelled fetch");
        fetchCts?.Cancel();
        propertyManager.CanCancelFetch = false;
        propertyManager.ShowProgress = false;
        propertyManager.HasFetchError = false;
        propertyManager.IsBusy = false;
        // Allow screen to turn off when user cancels
        DeviceDisplay.Current.KeepScreenOn = false;
        // Close the modal after canceling the fetch
        await navigationService.PopModalAsync();
    }

    private async Task RetryFetchAsync()
    {
        propertyManager.HasFetchError = false;
        await RefreshFromState();
        if (!propertyManager.HasFetchError)
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
    }

    public FlowDirection ContentFlowDirection
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var direction = currentSchedule?.MusicLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
            return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
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

    public bool CanCancelFetch
    {
        get => propertyManager.CanCancelFetch;
        set => propertyManager.CanCancelFetch = value;
    }

    public bool HasFetchError
    {
        get => propertyManager.HasFetchError;
        set => propertyManager.HasFetchError = value;
    }

    /// <summary>Show cancel (and retry when HasFetchError) button in overlay.</summary>
    public bool ShowCancelButton => ShowProgress || HasFetchError;

    /// <summary>
    /// Initialize is called via Task.Run from HandleMusicInitialized.
    /// Uses semaphore to serialize with RefreshFromState calls.
    /// </summary>
    private async Task Initialize()
    {
        // Serialize with RefreshFromState calls
        await refreshSemaphore.WaitAsync();
        try
        {
            await InitializeInternal();
        }
        finally
        {
            refreshSemaphore.Release();
        }
    }

    private async Task InitializeInternal()
    {
        var current = stateManager.Current;
        if (current == null)
        {
            return;
        }

        // Handle instrumental music (no language needed)
        // Music type is inferred from LanguageCode: null = melody/instrumental
        if (string.IsNullOrEmpty(current.LanguageCode))
        {
            await PopulateSongPublications(null); // null language code for instrumental music
            return;
        }

        // Handle vocal music (requires language)
        var languageCode = current.LanguageCode;

        if (languageCode == null)
        {
            var languages = await mediaService.GetVocalMusicLanguages();
            // Default to English for Vocals, fallback to first available if English not present
            languageCode = languages.ContainsKey(AppConstants.Media.DefaultLanguageCode) ? AppConstants.Media.DefaultLanguageCode : languages.FirstOrDefault().Key;
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }
            current.LanguageCode = languageCode;
        }

        // Only populate languages if not already populated (avoids duplicate population during modal open)
        if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
        {
            await PopulateLanguages();
        }
        
        // When initializing, don't download all publications yet (only first publication in cascade)
        await PopulateSongPublications(languageCode, downloadAll: false);

        propertyManager.SetupLanguageSearchHandler(async (searchTerm) => await PopulateLanguages(searchTerm));
    }

    private async Task PopulateLanguages(string? searchTerm = null)
    {
        // Use same effective current as RefreshLanguagesAsync so selection is correct for melody (E) and vocal.
        var currentSchedule = state.Value.CurrentSchedule;
        var currentLanguageCode = currentSchedule?.MusicLanguageCode;
        var effectiveLanguageCode = !string.IsNullOrEmpty(currentLanguageCode) ? currentLanguageCode : "E";
        var effectiveCurrent = new AlarmMusic { LanguageCode = effectiveLanguageCode };

        await dataProvider.PopulateLanguages(
            effectiveCurrent,
            propertyManager.Languages,
            lang => propertyManager.CurrentLanguage = lang,
            searchTerm);
    }

    /// <summary>
    /// Refreshes only the languages list for the language selection modal.
    /// This is a simpler refresh that only populates languages, not publications.
    /// </summary>
    public async Task RefreshLanguagesAsync()
    {
        try
        {
            // Get current language code from state (more reliable than stateManager.Current)
            var currentSchedule = state.Value.CurrentSchedule;
            var currentLanguageCode = currentSchedule?.MusicLanguageCode;

            // For melody (MusicLanguageCode null), use effective default language so the language modal
            // marks English as selected and scroll-to-selected works (same UX as Bible language modal).
            var effectiveLanguageCode = !string.IsNullOrEmpty(currentLanguageCode)
                ? currentLanguageCode
                : AppConstants.Media.DefaultLanguageCode;

            var tempCurrent = new AlarmMusic { LanguageCode = effectiveLanguageCode };

            Serilog.Log.Debug("MusicPublicationSelectionViewModel.RefreshLanguagesAsync: currentLanguageCode={LanguageCode}, effectiveLanguageCode={Effective}",
                currentLanguageCode ?? "(null)", effectiveLanguageCode);

            // Populate languages for the language modal
            await dataProvider.PopulateLanguages(
                tempCurrent,
                propertyManager.Languages,
                lang => propertyManager.CurrentLanguage = lang,
                null);

            // Attach search handler so typing in the Language modal filters the list (same as Bible language modal).
            propertyManager.SetupLanguageSearchHandler(async (searchTerm) => await PopulateLanguages(searchTerm));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "MusicPublicationSelectionViewModel: Error refreshing languages");
        }
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures languages are populated and current is initialized from CurrentSchedule.
    /// </summary>
    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// Uses a semaphore to ensure concurrent calls (from fire-and-forget Initialize and ModalScrollHelper) wait for each other.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Serialize RefreshFromState/Initialize calls - if one is in progress, wait for it to complete
        // This fixes the race condition where fire-and-forget Initialize races with ModalScrollHelper's refresh call
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
        // Cancel any previous fetch and create new cancellation token
        fetchCts?.Cancel();
        fetchCts = new CancellationTokenSource();
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            propertyManager.IsBusy = true;
            propertyManager.CanCancelFetch = true;
            propertyManager.HasFetchError = false;
            // Keep screen on during download to prevent Android from restricting network access
            DeviceDisplay.Current.KeepScreenOn = true;
        });

        // Wait for state to be updated (in case language was just changed)
        // This handles the race condition where the modal opens before state is fully updated
        // Use CurrentSchedule as primary source
        const int maxWaitAttempts = 10;
        const int delayMs = 100;
        string? newLanguageCode = null;
        bool isMelodyMusic = false; // inferred from LanguageCode being null

        try
        {
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                // Check for cancellation before each retry attempt
                fetchCts.Token.ThrowIfCancellationRequested();
                
                var stateValue = state.Value;

                // Use CurrentSchedule as the source of truth
                if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
                {
                    newLanguageCode = stateValue.CurrentSchedule.MusicLanguageCode;
                    isMelodyMusic = string.IsNullOrEmpty(newLanguageCode);
                    
                    // For Vocals, we need language code; for Melodies, it can be null
                    if (!isMelodyMusic && !string.IsNullOrEmpty(newLanguageCode))
                    {
                        break;
                    }
                    else if (isMelodyMusic)
                    {
                        // For Melodies, language code is null, so we can proceed
                        break;
                    }
                }

                // Wait a bit and retry if language code is not set yet (for Vocals) - with cancellation support
                await Task.Delay(delayMs, fetchCts.Token);
            }

            var finalStateValue = state.Value;
            if (finalStateValue.CurrentSchedule == null)
            {
                return;
            }

            var current = stateManager.GetCurrentFromState(state, mapper);
            if (current != null)
            {
                stateManager.EnsureCurrentIsSet(state, mapper);
            }

            // For vocal music (has language code), ensure languages are populated
            if (!isMelodyMusic)
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
            if (!isMelodyMusic &&
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
                    languageToSelect = propertyManager.Languages.FirstOrDefault(l => l.Code == AppConstants.Media.DefaultLanguageCode)
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

            // Opening the publications modal is the ONLY time we download ALL publications for a language.
            // Always use downloadAll=true here so placeholders can be hydrated into localized names.
            // Don't set ShowProgress here - let PopulateSongPublications control it via progress tracker
            // This prevents progress from showing when no fetch is needed (e.g., English language)
            
            // Create progress tracker with cancellation support
            // The progress tracker will set ShowProgress = true when a fetch actually starts
            var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                progress => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ProgressPercent = progress),
                text => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ProgressText = text),
                isVisible => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ShowProgress = isVisible),
                fetchCts.Token);

            // For instrumental music (no language), populate publications directly
            if (isMelodyMusic)
            {
                // null language code for instrumental music
                await PopulateSongPublications(null, downloadAll: true, progressTracker, fetchCts.Token);
            }
            // For vocal music, always fetch ALL publications when the modal opens (downloadAll=true).
            else if (!string.IsNullOrEmpty(languageCodeToUse))
            {
                await PopulateSongPublications(languageCodeToUse, downloadAll: true, progressTracker, fetchCts.Token);
            }
            else
            {
                SetSelectedSongPublication();
            }

            SetSelectedSongPublication();
        }
        catch (OperationCanceledException)
        {
            // Fetch was cancelled - data saved so far is preserved
            Serilog.Log.Debug("MusicPublicationSelectionViewModel: Fetch cancelled by user");
            // Hide progress overlay when cancelled
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                propertyManager.ShowProgress = false;
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
        {
            Serilog.Log.Warning(ex, "MusicPublicationSelectionViewModel: Fetch failed with network error");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                propertyManager.ShowProgress = false;
                propertyManager.HasFetchError = true;
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "MusicPublicationSelectionViewModel: Fetch failed during refresh");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                propertyManager.ShowProgress = false;
                propertyManager.HasFetchError = true;
            });
        }
        finally
        {
            // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                propertyManager.CanCancelFetch = false;
                propertyManager.ShowProgress = false;
                // Allow screen to turn off after download completes or fails
                DeviceDisplay.Current.KeepScreenOn = false;
            });
        }
    }

    private async Task PopulateSongPublications(string? languageCode, bool downloadAll = false, IFetchProgress? progress = null, CancellationToken cancellationToken = default)
    {
        await dataProvider.PopulateSongPublications(
            languageCode,
            stateManager.Current,
            propertyManager.SongPublications,
            songPublication => propertyManager.SelectedSongPublication = songPublication,
            downloadAll,
            progress,
            cancellationToken);
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

        // Cancel any ongoing fetch
        fetchCts?.Cancel();
        fetchCts?.Dispose();
        fetchCts = null;
        
        // Dispose semaphore
        refreshSemaphore.Dispose();

        if (propertyManagerPropertyChangedHandler != null)
        {
            propertyManager.PropertyChanged -= propertyManagerPropertyChangedHandler;
            propertyManagerPropertyChangedHandler = null;
        }
    }

    private void SetupPropertyManagerForwarding()
    {
        // The view binds to THIS ViewModel (not the property manager).
        // Forward property-manager changes so bindings update (busy overlay + modal progress indicator).
        propertyManagerPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.IsBusy))
            {
                OnPropertyChanged(nameof(IsBusy));
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.ShowProgress))
            {
                OnPropertyChanged(nameof(ShowProgress));
                OnPropertyChanged(nameof(ShowCancelButton));
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.ProgressPercent))
            {
                OnPropertyChanged(nameof(ProgressPercent));
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.ProgressText))
            {
                OnPropertyChanged(nameof(ProgressText));
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.CanCancelFetch))
            {
                OnPropertyChanged(nameof(CanCancelFetch));
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.HasFetchError))
            {
                OnPropertyChanged(nameof(HasFetchError));
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        };

        propertyManager.PropertyChanged += propertyManagerPropertyChangedHandler;
    }
}
