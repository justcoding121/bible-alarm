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
        // Use CurrentSchedule as the source of truth, with CurrentBibleReadingSchedule as fallback
        var currentState = state.Value;
        BibleReadingSchedule? initialCurrent = null;
        string? initialLanguageCode = null;
        if (currentState.CurrentBibleReadingSchedule != null)
        {
            initialCurrent = mapper.Map<BibleReadingSchedule>(currentState.CurrentBibleReadingSchedule);
            initialLanguageCode = initialCurrent.LanguageCode;
        }
        else if (currentState.CurrentSchedule != null && !string.IsNullOrEmpty(currentState.CurrentSchedule.BibleReadingLanguageCode))
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            var currentSchedule = currentState.CurrentSchedule;
            initialCurrent = new BibleReadingSchedule
            {
                LanguageCode = currentSchedule.BibleReadingLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode ?? string.Empty,
                SectionNumber = currentSchedule.BibleReadingSectionNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            initialLanguageCode = initialCurrent.LanguageCode;
        }
        stateHandler.InitializeCurrent(initialCurrent, initialLanguageCode);

        // Set up event handlers
        state.StateChanged += OnBibleReadingInitialized;
        state.StateChanged += OnBibleReadingChanged;

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

        // Always trigger initialization, even if CurrentBibleReadingSchedule is null
        // This ensures languages are populated for the language modal use case
        OnBibleReadingInitialized(null, EventArgs.Empty);
    }

    private async void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        await stateHandler.HandleBibleReadingInitializedAsync(
            busy => propertyManager.IsBusy = busy,
            propertyManager.Languages,
            state.Value.CurrentSchedule?.BibleReadingLanguageCode,
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
        await stateHandler.HandleBibleReadingInitializedAsync(
            busy => propertyManager.IsBusy = busy,
            propertyManager.Languages,
            state.Value.CurrentSchedule?.BibleReadingLanguageCode,
            () => propertyManager.UpdateCurrentLanguageFromLanguages());

        // Only populate translations if we have a current language (for full Bible selection, not language modal)
        if (!string.IsNullOrEmpty(state.Value.CurrentSchedule?.BibleReadingLanguageCode))
        {
            await stateHandler.RefreshFromStateAsync(
                busy => propertyManager.IsBusy = busy,
                propertyManager.Translations);
        }
    }

    private async void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        await stateHandler.HandleBibleReadingChangedAsync(
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
        state.StateChanged -= OnBibleReadingInitialized;
        state.StateChanged -= OnBibleReadingChanged;

        // Clean up property manager
        propertyManager.Cleanup();
    }
}
