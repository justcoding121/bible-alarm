#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationSelectionViewModel : ObservableObject, IListViewModel, IHasFetchErrorListViewModel, IDisposable
{
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;
    private readonly INavigationService navigationService;

    // Services
    private readonly BiblePublicationSelectionCommandHandler commandHandler;
    private readonly BiblePublicationSelectionStateHandler stateHandler;
    private readonly BiblePublicationSelectionDataProvider dataProvider;
    private readonly BiblePublicationSelectionPropertyManager propertyManager;
    private PropertyChangedEventHandler? propertyManagerPropertyChangedHandler;

    // Cancellation support for fetch operations
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState calls - ensures concurrent calls wait for each other
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

    public ICommand BackCommand { get; set; }
    public ICommand SectionSelectionCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }
    public ICommand CancelFetchCommand { get; }
    public ICommand RetryFetchCommand { get; }

    public BiblePublicationSelectionViewModel(
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper,
        IServiceProvider serviceProvider,
        IBiblePublicationService? biblePublicationService = null)
    {
        this.state = state;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;
        this.navigationService = navigationService;

        // Initialize services
        dataProvider = new BiblePublicationSelectionDataProvider(mediaService, state, dispatcher);
        stateHandler = new BiblePublicationSelectionStateHandler(mediaService, state, mapper, dataProvider, scopeFactory);
        var languageContentService = serviceProvider.GetService<ILanguageContentService>();
        commandHandler = new BiblePublicationSelectionCommandHandler(mediaService, state, dispatcher, navigationService, mapper, biblePublicationService, languageContentService);
        propertyManager = new BiblePublicationSelectionPropertyManager(state, dataProvider, stateHandler);
        SetupPropertyManagerForwarding();

        // Initialize current from state if available (map DTO to entity)
        // Use CurrentSchedule as the source of truth
        var currentState = state.Value;
        BiblePublicationSchedule? initialCurrent = null;
        string? initialLanguageCode = null;
        string? initialCategoryName = null;
        if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.BiblePublicationCode))
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            var currentSchedule = currentState.CurrentSchedule;
            initialCurrent = new BiblePublicationSchedule
            {
                // Language can be empty for publications without language (e.g., "iam")
                LanguageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionCode,
                TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty
            };
            initialLanguageCode = initialCurrent.LanguageCode;
            initialCategoryName = currentSchedule.BiblePublicationCategoryName;
        }
        else if (currentState.CurrentSchedule != null)
        {
            // Even if language code is not set, we should still track the category
            initialCategoryName = currentState.CurrentSchedule.BiblePublicationCategoryName;
        }
        stateHandler.InitializeCurrent(initialCurrent, initialLanguageCode, initialCategoryName);

        // Set up event handlers
        state.StateChanged += OnBiblePublicationInitialized;
        state.StateChanged += OnBiblePublicationChanged;

        // Initialize commands
        SectionSelectionCommand = commandHandler.CreateSectionSelectionCommand(
            () => propertyManager.CurrentLanguage,
            () => propertyManager.Publications,
            () => dataProvider.GetPublicationVMsMapping(),
            () => stateHandler.Current,
            isVisible => propertyManager.ShowProgress = isVisible,
            progress => propertyManager.ProgressPercent = progress,
            text => propertyManager.ProgressText = text,
            busy => propertyManager.IsBusy = busy);

        BackCommand = commandHandler.CreateBackCommand();
        CloseModalCommand = commandHandler.CreateCloseModalCommand();

        SelectLanguageCommand = commandHandler.CreateSelectLanguageCommand(
            () => propertyManager.Languages,
            () => dataProvider.GetPublicationVMsMapping(),
            language => propertyManager.UpdateSelectedLanguage(language),
            isVisible => propertyManager.ShowProgress = isVisible,
            progress => propertyManager.ProgressPercent = progress,
            text => propertyManager.ProgressText = text,
            busy => propertyManager.IsBusy = busy);

        // Cancel and retry fetch commands
        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);
        RetryFetchCommand = new AsyncRelayCommand(RetryFetchAsync);

        // Always trigger initialization, even if CurrentBiblePublicationSchedule is null
        // This ensures languages are populated for the language modal use case
        OnBiblePublicationInitialized(null, EventArgs.Empty);
    }

    private async Task CancelFetchAsync()
    {
        Serilog.Log.Information("BiblePublicationSelectionViewModel: CancelFetchCommand - User cancelled fetch");
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

    private async void OnBiblePublicationInitialized(object? o, EventArgs eventArgs)
    {
        await stateHandler.HandleBiblePublicationInitializedAsync(
            busy => propertyManager.IsBusy = busy,
            propertyManager.Languages,
            state.Value.CurrentSchedule?.BiblePublicationLanguageCode,
            () => propertyManager.UpdateCurrentLanguageFromLanguages());

        // Set up property changed handler for language search
        propertyManager.SetupPropertyChangedHandler(searchTerm =>
            _ = dataProvider.PopulateLanguagesAsync(searchTerm, propertyManager.Languages));
    }

    /// <summary>
    /// Refreshes only the languages list for the language selection modal.
    /// This is a simpler refresh that only populates languages, not publications.
    /// </summary>
    public async Task RefreshLanguagesAsync()
    {
        try
        {
            // Populate languages for the language modal
            await dataProvider.PopulateLanguagesAsync(null, propertyManager.Languages);
            
            // Update CurrentLanguage after population so scroll-to-selected works
            propertyManager.UpdateCurrentLanguageFromLanguages();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "BiblePublicationSelectionViewModel: Error refreshing languages");
        }
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures publications are populated and current is initialized from CurrentSchedule.
    /// NOTE: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper.
    /// On fetch error, sets HasFetchError = true instead of closing the modal.
    /// Uses a semaphore to serialize concurrent calls.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Serialize RefreshFromState calls - if one is in progress, wait for it to complete
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
        propertyManager.CanCancelFetch = true;
        propertyManager.HasFetchError = false;
        // Don't set ShowProgress here - let PopulatePublicationsAsync control it via progress tracker
        // This prevents progress from showing when no fetch is needed (e.g., English language)
        
        // Keep screen on during download to prevent Android from restricting network access
        DeviceDisplay.Current.KeepScreenOn = true;

        // Create progress tracker with cancellation support
        // The progress tracker will set ShowProgress = true when a fetch actually starts
        var progressTracker = new FetchProgressTracker(
            progress => propertyManager.ProgressPercent = progress,
            text => propertyManager.ProgressText = text,
            isVisible => propertyManager.ShowProgress = isVisible,
            fetchCts.Token);

        try
        {
            // Populate publications. ModalScrollHelper sets IsBusy = false after list is rendered.
            await stateHandler.RefreshFromStateAsync(
                busy => propertyManager.IsBusy = busy,
                propertyManager.Publications,
                progressTracker);
            
            // Set the selected publication after population so scroll-to-selected works
            propertyManager.SetSelectedPublication();
        }
        catch (OperationCanceledException)
        {
            // Fetch was cancelled - data saved so far is preserved
            Serilog.Log.Debug("BiblePublicationSelectionViewModel: Fetch cancelled by user");
            // Hide progress overlay when cancelled
            propertyManager.ShowProgress = false;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
        {
            Serilog.Log.Warning(ex, "BiblePublicationSelectionViewModel: Fetch failed with network error");
            propertyManager.ShowProgress = false;
            propertyManager.HasFetchError = true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "BiblePublicationSelectionViewModel: Fetch failed during refresh");
            propertyManager.ShowProgress = false;
            propertyManager.HasFetchError = true;
        }
        finally
        {
            propertyManager.CanCancelFetch = false;
            // Ensure progress overlay is hidden after operation completes
            // This handles cases where no fetch was needed (e.g., English language packaged with app)
            // and the progress tracker didn't call SetIsVisible(false)
            if (propertyManager.ShowProgress)
            {
                propertyManager.ShowProgress = false;
            }
            // Allow screen to turn off after download completes or fails
            DeviceDisplay.Current.KeepScreenOn = false;
        }
    }

    private async void OnBiblePublicationChanged(object? sender, EventArgs e)
    {
        await stateHandler.HandleBiblePublicationChangedAsync(
            busy => propertyManager.IsBusy = busy,
            () => propertyManager.SetSelectedPublication(),
            propertyManager.Publications);
    }

    // Properties delegated to property manager
    public ObservableCollection<PublicationListViewItemModel> Publications => propertyManager.Publications;
    public ObservableCollection<LanguageListViewItemModel> Languages => propertyManager.Languages;
    public PublicationListViewItemModel? SelectedPublication { get => propertyManager.SelectedPublication; set => propertyManager.SelectedPublication = value; }
    public LanguageListViewItemModel? CurrentLanguage { get => propertyManager.CurrentLanguage; set => propertyManager.CurrentLanguage = value; }
    public bool IsBusy { get => propertyManager.IsBusy; set => propertyManager.IsBusy = value; }
    public string PublicationCode { get => propertyManager.PublicationCode; set => propertyManager.PublicationCode = value; }
    public string LanguageSearchTerm { get => propertyManager.LanguageSearchTerm; set => propertyManager.LanguageSearchTerm = value; }
    public object? SelectedItem => propertyManager.SelectedItem;
    public bool ShowProgress { get => propertyManager.ShowProgress; set => propertyManager.ShowProgress = value; }
    public double ProgressPercent { get => propertyManager.ProgressPercent; set => propertyManager.ProgressPercent = value; }
    public string ProgressText { get => propertyManager.ProgressText; set => propertyManager.ProgressText = value; }
    public bool CanCancelFetch { get => propertyManager.CanCancelFetch; set => propertyManager.CanCancelFetch = value; }
    public bool HasFetchError { get => propertyManager.HasFetchError; set => propertyManager.HasFetchError = value; }

    /// <summary>Show cancel (and retry when HasFetchError) button in overlay.</summary>
    public bool ShowCancelButton => ShowProgress || HasFetchError;

    /// <summary>
    /// Gets the FlowDirection for content based on the selected language direction.
    /// </summary>
    public FlowDirection ContentFlowDirection
    {
        get
        {
            var direction = state.Value.CurrentSchedule?.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
            return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }
    }



    public void Dispose()
    {
        state.StateChanged -= OnBiblePublicationInitialized;
        state.StateChanged -= OnBiblePublicationChanged;

        // Cancel any ongoing fetch
        fetchCts?.Cancel();
        fetchCts?.Dispose();
        fetchCts = null;
        
        // Dispose semaphore
        refreshSemaphore.Dispose();

        // Clean up property manager
        propertyManager.Cleanup();

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
            if (e.PropertyName == nameof(BiblePublicationSelectionPropertyManager.IsBusy))
            {
                OnPropertyChanged(nameof(IsBusy));
                return;
            }

            if (e.PropertyName == nameof(BiblePublicationSelectionPropertyManager.ShowProgress))
            {
                OnPropertyChanged(nameof(ShowProgress));
                OnPropertyChanged(nameof(ShowCancelButton));
                return;
            }

            if (e.PropertyName == nameof(BiblePublicationSelectionPropertyManager.ProgressPercent))
            {
                OnPropertyChanged(nameof(ProgressPercent));
                return;
            }

            if (e.PropertyName == nameof(BiblePublicationSelectionPropertyManager.ProgressText))
            {
                OnPropertyChanged(nameof(ProgressText));
                return;
            }

            if (e.PropertyName == nameof(BiblePublicationSelectionPropertyManager.CanCancelFetch))
            {
                OnPropertyChanged(nameof(CanCancelFetch));
                return;
            }

            if (e.PropertyName == nameof(BiblePublicationSelectionPropertyManager.HasFetchError))
            {
                OnPropertyChanged(nameof(HasFetchError));
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        };

        propertyManager.PropertyChanged += propertyManagerPropertyChangedHandler;
    }
}
