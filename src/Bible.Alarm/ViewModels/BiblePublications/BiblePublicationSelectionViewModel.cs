#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed partial class BiblePublicationSelectionViewModel : ObservableObject, IHasFetchErrorListViewModel, IRecipient<ListItemFetchProgressMessage>, IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
{
    private readonly IState<ApplicationState> state;
    private readonly INavigationService navigationService;

    // Services
    private readonly BiblePublicationSelectionStateHandler stateHandler;
    private readonly BiblePublicationSelectionDataProvider dataProvider;

    private ObservableCollection<PublicationListViewItemModel>? publications;
    private ObservableCollection<LanguageListViewItemModel>? languages;
    private LanguageListViewItemModel? currentLanguage;
    private bool isBusy = true;
    private string languageSearchTerm = string.Empty;
    private bool showProgress;
    private double progressPercent;
    private string progressText = "0%";
    private bool canCancelFetch;
    private bool hasFetchError;
    private bool isCancelBusy;
    private PropertyChangedEventHandler? languageSearchHandler;

    // Cancellation support for fetch operations
    private CancellationTokenSource? fetchCts;
    
    // Semaphore to serialize RefreshFromState calls - ensures concurrent calls wait for each other
    private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

    public ICommand BackCommand { get; set; }
    public ICommand SectionSelectionCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }
    public ICommand CancelFetchCommand { get; }

    public BiblePublicationSelectionViewModel(BiblePublicationSelectionViewModelDeps deps)
    {
        state = deps.ApplicationState;
        navigationService = deps.NavigationService;

        var serviceProvider = deps.ServiceProvider;
        var languageNameService = serviceProvider.GetRequiredService<ILanguageNameService>();
        dataProvider = new BiblePublicationSelectionDataProvider(deps.MediaService, languageNameService, state, deps.Dispatcher);
        stateHandler = new BiblePublicationSelectionStateHandler(state, dataProvider, deps.ScopeFactory);
        var languageContentService = serviceProvider.GetService<ILanguageContentService>();
        var commandHandler = new BiblePublicationSelectionCommandHandler(deps.MediaService, state, deps.Dispatcher, navigationService, deps.BiblePublicationService, languageContentService);

        // CurrentSchedule is the source of truth
        var currentState = state.Value;
        BiblePublicationSchedule? initialCurrent = null;
        string? initialLanguageCode = null;
        string? initialCategoryName = null;
        if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.BiblePublicationCode))
        {
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

        state.StateChanged += OnBiblePublicationInitialized;
        state.StateChanged += OnBiblePublicationChanged;

        WeakReferenceMessenger.Default.Register<ListItemFetchProgressMessage>(this);
        WeakReferenceMessenger.Default.Register<ModalOverlayFetchProgressMessage>(this);

        SectionSelectionCommand = commandHandler.CreateSectionSelectionCommand(
            new SectionSelectionSelectors(
                () => CurrentLanguage,
                () => Publications,
                () => dataProvider.GetPublicationVMsMapping(),
                () => stateHandler.Current),
            new SectionSelectionUiBindings(
                isVisible => ShowProgress = isVisible,
                progress => ProgressPercent = progress,
                text => ProgressText = text,
                busy => IsBusy = busy));

        BackCommand = commandHandler.CreateBackCommand();
        CloseModalCommand = commandHandler.CreateCloseModalCommand();

        SelectLanguageCommand = commandHandler.CreateSelectLanguageCommand(
            () => Languages,
            () => dataProvider.GetPublicationVMsMapping(),
            UpdateSelectedLanguage,
            isVisible => ShowProgress = isVisible,
            progress => ProgressPercent = progress,
            text => ProgressText = text,
            busy => IsBusy = busy);

        // Cancel and retry fetch commands
        CancelFetchCommand = new AsyncRelayCommand(CancelFetchAsync);
        // Always trigger initialization, even if CurrentBiblePublicationSchedule is null
        // This ensures languages are populated for the language modal use case
        OnBiblePublicationInitialized(null, EventArgs.Empty);
    }

    private async Task CancelFetchAsync()
    {
        IsCancelBusy = true;
        await Task.Delay(50);

        try
        {
            Serilog.Log.Information(AppConstants.Logging.BiblePublicationSelectionViewModelDiagnosticsLog.CancelFetchCommandUserCancelledFetch);
            fetchCts?.CancelAsync();
            CanCancelFetch = false;
            ShowProgress = false;
            IsBusy = false;
            DeviceDisplay.Current.KeepScreenOn = false;
            await navigationService.PopModalAsync();
        }
        finally
        {
            IsCancelBusy = false;
        }
    }

    private async void OnBiblePublicationInitialized(object? o, EventArgs eventArgs)
    {
        await stateHandler.HandleBiblePublicationInitializedAsync(
            busy => IsBusy = busy,
            Languages,
            state.Value.CurrentSchedule?.BiblePublicationLanguageCode,
            UpdateCurrentLanguageFromLanguages);

        SetupLanguageSearchHandler(searchTerm =>
            _ = dataProvider.PopulateLanguagesAsync(searchTerm, Languages));
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
            await dataProvider.PopulateLanguagesAsync(null, Languages);

            // Update CurrentLanguage after population so scroll-to-selected works
            UpdateCurrentLanguageFromLanguages();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionViewModelDiagnosticsLog.ErrorRefreshingLanguages);
        }
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures publications are populated and current is initialized from CurrentSchedule.
    /// NOTE: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper.
    /// On fetch error, rethrows so ModalScrollHelper can close modal and show toast; state is retained.
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
        fetchCts?.CancelAsync();
        fetchCts = new CancellationTokenSource();
        CanCancelFetch = true;
        // Do not set ShowProgress here - let the progress reporter control it only when a fetch is actually decided
        // (e.g. English pre-packaged pubs skip fetch and never show overlay)

        // Keep screen on during download to prevent Android from restricting network access
        DeviceDisplay.Current.KeepScreenOn = true;

        var progressReporter = new ModalOverlayFetchProgressReporter("BiblePublication", fetchCts.Token);

        try
        {
            // Populate publications.
            await stateHandler.RefreshFromStateAsync(
                busy => IsBusy = busy,
                Publications,
                progressReporter);

            // Set the selected publication after population so scroll-to-selected works
            SetSelectedPublication();
        }
        catch (OperationCanceledException ex)
        {
            // Fetch was cancelled - data saved so far is preserved
            Serilog.Log.Debug(ex, AppConstants.Logging.BiblePublicationSelectionViewModelDiagnosticsLog.FetchCancelledByUser);
            // Hide progress overlay when cancelled
            ShowProgress = false;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() => ShowProgress = false);
            throw new InvalidOperationException(
                AppConstants.Logging.BiblePublicationSelectionViewModelDiagnosticsLog.FetchFailedNetworkError,
                ex);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => ShowProgress = false);
            throw new InvalidOperationException(
                AppConstants.Logging.BiblePublicationSelectionViewModelDiagnosticsLog.FetchFailedDuringRefresh,
                ex);
        }
        finally
        {
            CanCancelFetch = false;
            // Ensure progress overlay is hidden after operation completes
            // This handles cases where no fetch was needed (e.g., English language packaged with app)
            // and the progress tracker didn't call SetIsVisible(false)
            if (ShowProgress)
            {
                ShowProgress = false;
            }
            // Allow screen to turn off after download completes or fails
            DeviceDisplay.Current.KeepScreenOn = false;
        }
    }

    private async void OnBiblePublicationChanged(object? sender, EventArgs e)
    {
        await stateHandler.HandleBiblePublicationChangedAsync(
            busy => IsBusy = busy,
            SetSelectedPublication,
            Publications);
    }

    public ObservableCollection<PublicationListViewItemModel> Publications
    {
        get => publications ??= [];
        set => SetProperty(ref publications, value);
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public PublicationListViewItemModel? SelectedPublication { get; set; }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        }
    }

    public string PublicationCode
    {
        get => stateHandler.Current?.PublicationCode ?? "";
        set
        {
            if (stateHandler.Current == null)
            {
                return;
            }

            stateHandler.Current.PublicationCode = value;
            OnPropertyChanged();
        }
    }

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public object? SelectedItem => CurrentLanguage;

    public bool ShowProgress
    {
        get => showProgress;
        set
        {
            if (SetProperty(ref showProgress, value))
            {
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        }
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

    public bool CanCancelFetch
    {
        get => canCancelFetch;
        set => SetProperty(ref canCancelFetch, value);
    }

    public bool HasFetchError
    {
        get => hasFetchError;
        set
        {
            if (SetProperty(ref hasFetchError, value))
            {
                OnPropertyChanged(nameof(ShowCancelButton));
            }
        }
    }

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

    /// <summary>Show cancel button in overlay during fetch or when busy loading.</summary>
    public bool ShowCancelButton => ShowProgress || IsBusy;

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

    private void UpdateCurrentLanguageFromLanguages()
    {
        if (Languages != null)
        {
            var selectedLanguages = Languages.Where(l => l.IsSelected).ToList();
            var selectedLanguage = Languages.FirstOrDefault(l => l.IsSelected);

            if (selectedLanguages.Count > 1)
            {
                Serilog.Log.Warning(AppConstants.Logging.BiblePublicationSelectionLanguageDiagnosticsLog.MultipleLanguagesSelectedCount,
                    selectedLanguages.Count);
            }

            if (selectedLanguage != null)
            {
                CurrentLanguage = selectedLanguage;
            }
        }
    }

    private void SetupLanguageSearchHandler(Action<string?> populateLanguages)
    {
        if (languageSearchHandler != null)
        {
            PropertyChanged -= languageSearchHandler;
        }

        languageSearchHandler = (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(LanguageSearchTerm), StringComparison.Ordinal))
            {
                populateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += languageSearchHandler;
    }

    private void UpdateSelectedLanguage(LanguageListViewItemModel language)
    {
        if (CurrentLanguage != null)
        {
            CurrentLanguage.IsSelected = false;
        }

        CurrentLanguage = language;
        CurrentLanguage!.IsSelected = true;
    }

    private void SetSelectedPublication()
    {
        // Use CurrentSchedule as the source of truth for publication code
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var publicationCode = stateValue.CurrentSchedule.BiblePublicationCode;
        
        // Clear ALL previous selections first to ensure only one publication is selected
        var mapping = dataProvider.GetPublicationVMsMapping();
        foreach (var pub in mapping.Values)
        {
            pub.IsSelected = false;
        }
        
        // Also clear the previous SelectedPublication
        if (SelectedPublication != null)
        {
            SelectedPublication.IsSelected = false;
        }

        if (string.IsNullOrEmpty(publicationCode))
        {
            SelectedPublication = null;
            return;
        }

        if (!mapping.TryGetValue(publicationCode, out var publication))
        {
            SelectedPublication = null;
            return;
        }

        SelectedPublication = publication;
        SelectedPublication!.IsSelected = true;
    }

    public void Receive(ListItemFetchProgressMessage message)
    {
        var p = message.Value;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.Equals(p.Context, "BibleLanguage", StringComparison.Ordinal))
            {
                var lang = Languages?.FirstOrDefault(l => string.Equals(l.Code, p.ItemId, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    lang.DownloadProgress = p.Progress;
            }
            else if (string.Equals(p.Context, "BiblePublication", StringComparison.Ordinal))
            {
                var pub = Publications?.FirstOrDefault(pr => string.Equals(pr.Code, p.ItemId, StringComparison.OrdinalIgnoreCase));
                if (pub != null)
                    pub.DownloadProgress = p.Progress;
            }
        });
    }

    public void Receive(ModalOverlayFetchProgressMessage message)
    {
        var p = message.Value;
        if (p.ModalType != "BiblePublication")
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (p.IsVisible)
            {
                ProgressPercent = p.Progress;
                ProgressText = p.ProgressText;
            }
            ShowProgress = p.IsVisible;
        });
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<ModalOverlayFetchProgressMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ListItemFetchProgressMessage>(this);
        state.StateChanged -= OnBiblePublicationInitialized;
        state.StateChanged -= OnBiblePublicationChanged;

        // Cancel any ongoing fetch
        fetchCts?.CancelAsync();
        fetchCts?.Dispose();
        fetchCts = null;
        
        // Dispose semaphore
        refreshSemaphore.Dispose();

        if (languageSearchHandler != null)
        {
            PropertyChanged -= languageSearchHandler;
            languageSearchHandler = null;
        }
    }
}
