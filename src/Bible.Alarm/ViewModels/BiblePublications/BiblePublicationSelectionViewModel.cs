#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Services
    private readonly BiblePublicationSelectionCommandHandler commandHandler;
    private readonly BiblePublicationSelectionStateHandler stateHandler;
    private readonly BiblePublicationSelectionDataProvider dataProvider;
    private readonly BiblePublicationSelectionPropertyManager propertyManager;
    private PropertyChangedEventHandler? propertyManagerPropertyChangedHandler;

    public ICommand BackCommand { get; set; }
    public ICommand SectionSelectionCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

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
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
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

        // Always trigger initialization, even if CurrentBiblePublicationSchedule is null
        // This ensures languages are populated for the language modal use case
        OnBiblePublicationInitialized(null, EventArgs.Empty);
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
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures languages and publications are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Always ensure languages are populated and IsBusy is set to false
        await stateHandler.HandleBiblePublicationInitializedAsync(
            busy => propertyManager.IsBusy = busy,
            propertyManager.Languages,
            state.Value.CurrentSchedule?.BiblePublicationLanguageCode,
            () => propertyManager.UpdateCurrentLanguageFromLanguages());

        // Only populate publications if we have a current language (for full Bible selection, not language modal)
        if (!string.IsNullOrEmpty(state.Value.CurrentSchedule?.BiblePublicationLanguageCode))
        {
            // Create progress tracker for modal open
            var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                progress => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ProgressPercent = progress),
                text => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ProgressText = text),
                isVisible => _ = MainThread.InvokeOnMainThreadAsync(() => propertyManager.ShowProgress = isVisible));
            
            await stateHandler.RefreshFromStateAsync(
                busy => propertyManager.IsBusy = busy,
                propertyManager.Publications,
                progressTracker);
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



    public void Dispose()
    {
        state.StateChanged -= OnBiblePublicationInitialized;
        state.StateChanged -= OnBiblePublicationChanged;

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
            }
        };

        propertyManager.PropertyChanged += propertyManagerPropertyChangedHandler;
    }
}
