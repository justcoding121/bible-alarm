#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;

/// <summary>
/// Handles initialization of commands for bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationCommandInitializer
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IScheduleSelectionService scheduleSelectionService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    public BiblePublicationCommandInitializer(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.scheduleSelectionService = scheduleSelectionService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;
    }

    public ICommand CreateSelectBibleTypeCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            logger.Information("BibleSelectionContainerViewModel: SelectBibleTypeCommand - Opening Bible type selection modal");
            // TODO: Implement BibleTypeSelectionModal similar to MusicSelectionModal
            // For now, this is a placeholder that logs the action
            // The modal will allow selecting between BiblePublication, BibleDrama, and DramaticReading
            await Task.CompletedTask;
            logger.Debug("BibleSelectionContainerViewModel: SelectBibleTypeCommand - Bible type selection not yet implemented");
        });
    }

    public ICommand CreateSelectLanguageCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            logger.Information("BibleSelectionContainerViewModel: SelectLanguageCommand - Opening language modal");
            // Create a temporary BibleSelectionViewModel instance for the language modal
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BiblePublicationSelectionViewModel>();
            logger.Debug("BibleSelectionContainerViewModel: SelectLanguageCommand - Created BibleSelectionViewModel, opening modal");
            await navigationService.OpenLanguageModalAsync(bibleSelectionViewModel);
            logger.Debug("BibleSelectionContainerViewModel: SelectLanguageCommand - Modal opened");
        });
    }

    public ICommand CreateSelectBibleCommand(Func<BiblePublicationSchedule?> getBiblePublication, Action<BiblePublicationSchedule?> setBiblePublication, int scheduleId, bool isNewSchedule, bool biblePublicationUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get Bible reading from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedBiblePublication = scheduleSelectionService.LoadBiblePublicationForSelection(
                scheduleId,
                isNewSchedule,
                getBiblePublication(),
                currentSchedule?.BiblePublicationLanguageCode,
                currentSchedule?.BiblePublicationCode,
                currentSchedule?.BiblePublicationSectionNumber,
                currentSchedule?.BiblePublicationTrackNumber,
                currentSchedule?.BiblePublicationFinishedDuration);

            setBiblePublication(loadedBiblePublication);

            // Create view model and open modal
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BiblePublicationSelectionViewModel>();
            await navigationService.OpenBibleSelectionModalAsync(bibleSelectionViewModel);

            // Map entities to DTOs before dispatching
            if (loadedBiblePublication != null)
            {
                var currentBiblePublicationItem = mapper.Map<BiblePublicationStateItem>(loadedBiblePublication);
                dispatcher.Dispatch(new BiblePublicationSelectionAction(currentBiblePublicationItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }

    public ICommand CreateSelectSectionCommand(Func<BiblePublicationSchedule?> getBiblePublication, Action<BiblePublicationSchedule?> setBiblePublication, int scheduleId, bool isNewSchedule, bool biblePublicationUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get Bible reading from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedBiblePublication = scheduleSelectionService.LoadBiblePublicationForSelection(
                scheduleId,
                isNewSchedule,
                getBiblePublication(),
                currentSchedule?.BiblePublicationLanguageCode,
                currentSchedule?.BiblePublicationCode,
                currentSchedule?.BiblePublicationSectionNumber,
                currentSchedule?.BiblePublicationTrackNumber,
                currentSchedule?.BiblePublicationFinishedDuration);

            setBiblePublication(loadedBiblePublication);

            // Create view model and open modal
            var sectionSelectionViewModel = serviceProvider.GetRequiredService<SectionSelectionViewModel>();
            await navigationService.OpenSectionSelectionModalAsync(sectionSelectionViewModel);

            // Map entities to DTOs before dispatching
            if (loadedBiblePublication != null)
            {
                var currentBiblePublicationItem = mapper.Map<BiblePublicationStateItem>(loadedBiblePublication);
                dispatcher.Dispatch(new SectionSelectionAction(currentBiblePublicationItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }

    public ICommand CreateSelectTrackCommand(Func<BiblePublicationSchedule?> getBiblePublication, Action<BiblePublicationSchedule?> setBiblePublication, int scheduleId, bool isNewSchedule, bool biblePublicationUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Get Bible reading from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedBiblePublication = scheduleSelectionService.LoadBiblePublicationForSelection(
                scheduleId,
                isNewSchedule,
                getBiblePublication(),
                currentSchedule?.BiblePublicationLanguageCode,
                currentSchedule?.BiblePublicationCode,
                currentSchedule?.BiblePublicationSectionNumber,
                currentSchedule?.BiblePublicationTrackNumber,
                currentSchedule?.BiblePublicationFinishedDuration);

            setBiblePublication(loadedBiblePublication);

            // Create view model and open modal
            var trackSelectionViewModel = serviceProvider.GetRequiredService<TrackSelectionViewModel>();
            await navigationService.OpenTrackSelectionModalAsync(trackSelectionViewModel);

            // Map entities to DTOs before dispatching
            if (loadedBiblePublication != null)
            {
                var currentBiblePublicationItem = mapper.Map<BiblePublicationStateItem>(loadedBiblePublication);
                dispatcher.Dispatch(new TrackSelectionAction(currentBiblePublicationItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }
}

