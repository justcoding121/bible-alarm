#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.DependencyInjection;
using Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

namespace Bible.Alarm.ViewModels.Bible;

public sealed class BibleSelectionViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    // Services
    private readonly BibleSelectionCommandHandler commandHandler;
    private readonly BibleSelectionStateHandler stateHandler;
    private readonly BibleSelectionDataProvider dataProvider;
    private readonly BibleSelectionPropertyManager propertyManager;

    public ICommand BackCommand { get; set; }
    public ICommand SectionSelectionCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SelectLanguageCommand { get; set; }

    public BibleSelectionViewModel(
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        this.state = state;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;

        // Initialize services
        dataProvider = new BibleSelectionDataProvider(mediaService, state, dispatcher);
        stateHandler = new BibleSelectionStateHandler(mediaService, state, mapper, dataProvider);
        commandHandler = new BibleSelectionCommandHandler(mediaService, state, dispatcher, navigationService, mapper);
        propertyManager = new BibleSelectionPropertyManager(state, dataProvider, stateHandler);

        // Initialize current from state if available (map DTO to entity)
        // Use CurrentSchedule as the source of truth, with CurrentBiblePublicationSchedule as fallback
        var currentState = state.Value;
        BiblePublicationSchedule? initialCurrent = null;
        string? initialLanguageCode = null;
        if (currentState.CurrentBiblePublicationSchedule != null)
        {
            initialCurrent = mapper.Map<BiblePublicationSchedule>(currentState.CurrentBiblePublicationSchedule);
            initialLanguageCode = initialCurrent.LanguageCode;
        }
        else if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.BiblePublicationLanguageCode))
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            var currentSchedule = currentState.CurrentSchedule;
            initialCurrent = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationPublicationCode ?? string.Empty,
                SectionNumber = currentSchedule.BiblePublicationSectionNumber ?? 1,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
            };
            initialLanguageCode = initialCurrent.LanguageCode;
        }
        stateHandler.InitializeCurrent(initialCurrent, initialLanguageCode);

        // Set up event handlers
        state.StateChanged += OnBiblePublicationInitialized;
        state.StateChanged += OnBiblePublicationChanged;

        // Initialize commands
        SectionSelectionCommand = commandHandler.CreateSectionSelectionCommand(
            () => propertyManager.CurrentLanguage,
            () => propertyManager.Translations,
            () => dataProvider.GetTranslationVMsMapping(),
            () => stateHandler.Current);

        BackCommand = commandHandler.CreateBackCommand();
        CloseModalCommand = commandHandler.CreateCloseModalCommand();

        SelectLanguageCommand = commandHandler.CreateSelectLanguageCommand(
            () => propertyManager.Languages,
            () => dataProvider.GetTranslationVMsMapping(),
            language => propertyManager.UpdateSelectedLanguage(language));

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
    /// This ensures languages and translations are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        // Always ensure languages are populated and IsBusy is set to false
        await stateHandler.HandleBiblePublicationInitializedAsync(
            busy => propertyManager.IsBusy = busy,
            propertyManager.Languages,
            state.Value.CurrentSchedule?.BiblePublicationLanguageCode,
            () => propertyManager.UpdateCurrentLanguageFromLanguages());

        // Only populate translations if we have a current language (for full Bible selection, not language modal)
        if (!string.IsNullOrEmpty(state.Value.CurrentSchedule?.BiblePublicationLanguageCode))
        {
            await stateHandler.RefreshFromStateAsync(
                busy => propertyManager.IsBusy = busy,
                propertyManager.Translations);
        }
    }

    private async void OnBiblePublicationChanged(object? sender, EventArgs e)
    {
        await stateHandler.HandleBiblePublicationChangedAsync(
            busy => propertyManager.IsBusy = busy,
            () => propertyManager.SetSelectedTranslation(),
            propertyManager.Translations);
    }

    // Properties delegated to property manager
    public ObservableCollection<PublicationListViewItemModel> Translations => propertyManager.Translations;
    public ObservableCollection<LanguageListViewItemModel> Languages => propertyManager.Languages;
    public PublicationListViewItemModel? SelectedTranslation { get => propertyManager.SelectedTranslation; set => propertyManager.SelectedTranslation = value; }
    public LanguageListViewItemModel? CurrentLanguage { get => propertyManager.CurrentLanguage; set => propertyManager.CurrentLanguage = value; }
    public bool IsBusy { get => propertyManager.IsBusy; set => propertyManager.IsBusy = value; }
    public string PublicationCode { get => propertyManager.PublicationCode; set => propertyManager.PublicationCode = value; }
    public string LanguageSearchTerm { get => propertyManager.LanguageSearchTerm; set => propertyManager.LanguageSearchTerm = value; }
    public object? SelectedItem => propertyManager.SelectedItem;



    public void Dispose()
    {
        state.StateChanged -= OnBiblePublicationInitialized;
        state.StateChanged -= OnBiblePublicationChanged;

        // Clean up property manager
        propertyManager.Cleanup();
    }
}
