#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Categories;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;

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

    public ICommand CreateSelectCategoryCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            logger.Information(AppConstants.Logging.BiblePublicationCommandInitializerDiagnosticsLog.SelectCategoryOpeningCategoryModal);
            var categoryViewModel = serviceProvider.GetRequiredService<CategorySelectionViewModel>();
            logger.Debug(AppConstants.Logging.BiblePublicationCommandInitializerDiagnosticsLog.SelectCategoryCreatedOpeningModal);
            await navigationService.OpenCategoryModalAsync(categoryViewModel);
            logger.Debug(AppConstants.Logging.BiblePublicationCommandInitializerDiagnosticsLog.SelectCategoryModalOpened);
        });
    }

    public ICommand CreateSelectLanguageCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            var mediaService = serviceProvider.GetRequiredService<IMediaService>();
            var categoryNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ICategoryNameService>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var displayTextProvider = new BiblePublicationDisplayTextProvider(state, logger, mediaService, categoryNameService, scopeFactory);
            if (!await displayTextProvider.GetIsLanguageSelectableAsync())
            {
                return;
            }

            logger.Information(AppConstants.Logging.BiblePublicationCommandInitializerDiagnosticsLog.SelectLanguageOpeningLanguageModal);
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BiblePublicationSelectionViewModel>();
            await navigationService.OpenLanguageModalAsync(bibleSelectionViewModel);
        });
    }

    public ICommand CreateSelectBibleCommand(Func<BiblePublicationSchedule?> getBiblePublication, Action<BiblePublicationSchedule?> setBiblePublication, int scheduleId, bool isNewSchedule, bool biblePublicationUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Check if publication is selectable (multiple options available)
            // If not selectable, don't open the modal
            var mediaService = serviceProvider.GetRequiredService<IMediaService>();
            var categoryNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ICategoryNameService>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var displayTextProvider = new BiblePublicationDisplayTextProvider(state, logger, mediaService, categoryNameService, scopeFactory);
            var isSelectable = await displayTextProvider.GetIsPublicationSelectableAsync();
            if (!isSelectable)
            {
                // Only one option available, don't open modal
                return;
            }

            // Get Bible reading from CurrentSchedule (already loaded from AlarmDB on page load)
            // No need to query AlarmDB again - only media index DB queries are needed for selection lists
            var currentSchedule = state.Value.CurrentSchedule;
            var loadedBiblePublication = scheduleSelectionService.LoadBiblePublicationForSelection(
                new LoadBiblePublicationForSelectionArgs(
                    scheduleId,
                    isNewSchedule,
                    getBiblePublication(),
                    new LoadBiblePublicationScheduleCodes(
                        currentSchedule?.BiblePublicationLanguageCode,
                        currentSchedule?.BiblePublicationCode,
                        currentSchedule?.BiblePublicationSectionCode,
                        currentSchedule?.BiblePublicationTrackCode),
                    currentSchedule?.BiblePublicationFinishedDuration));

            setBiblePublication(loadedBiblePublication);

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
            var mediaService = serviceProvider.GetRequiredService<IMediaService>();
            var categoryNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ICategoryNameService>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var displayTextProvider = new BiblePublicationDisplayTextProvider(state, logger, mediaService, categoryNameService, scopeFactory);
            if (!await displayTextProvider.GetIsSectionSelectableAsync())
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            var loadedBiblePublication = scheduleSelectionService.LoadBiblePublicationForSelection(
                new LoadBiblePublicationForSelectionArgs(
                    scheduleId,
                    isNewSchedule,
                    getBiblePublication(),
                    new LoadBiblePublicationScheduleCodes(
                        currentSchedule?.BiblePublicationLanguageCode,
                        currentSchedule?.BiblePublicationCode,
                        currentSchedule?.BiblePublicationSectionCode,
                        currentSchedule?.BiblePublicationTrackCode),
                    currentSchedule?.BiblePublicationFinishedDuration));

            setBiblePublication(loadedBiblePublication);

            var sectionSelectionViewModel = serviceProvider.GetRequiredService<BiblePublicationSectionSelectionViewModel>();
            await navigationService.OpenSectionSelectionModalAsync(sectionSelectionViewModel);

            // Map entities to DTOs before dispatching
            if (loadedBiblePublication != null)
            {
                var currentBiblePublicationItem = mapper.Map<BiblePublicationStateItem>(loadedBiblePublication);
                dispatcher.Dispatch(new BiblePublicationSectionSelectionAction(currentBiblePublicationItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }

    public ICommand CreateSelectTrackCommand(Func<BiblePublicationSchedule?> getBiblePublication, Action<BiblePublicationSchedule?> setBiblePublication, int scheduleId, bool isNewSchedule, bool biblePublicationUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            var mediaService = serviceProvider.GetRequiredService<IMediaService>();
            var categoryNameService = serviceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ICategoryNameService>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var displayTextProvider = new BiblePublicationDisplayTextProvider(state, logger, mediaService, categoryNameService, scopeFactory);
            if (!await displayTextProvider.GetIsTrackSelectableAsync())
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            var loadedBiblePublication = scheduleSelectionService.LoadBiblePublicationForSelection(
                new LoadBiblePublicationForSelectionArgs(
                    scheduleId,
                    isNewSchedule,
                    getBiblePublication(),
                    new LoadBiblePublicationScheduleCodes(
                        currentSchedule?.BiblePublicationLanguageCode,
                        currentSchedule?.BiblePublicationCode,
                        currentSchedule?.BiblePublicationSectionCode,
                        currentSchedule?.BiblePublicationTrackCode),
                    currentSchedule?.BiblePublicationFinishedDuration));

            setBiblePublication(loadedBiblePublication);

            var trackSelectionViewModel = serviceProvider.GetRequiredService<BiblePublicationTrackSelectionViewModel>();
            await navigationService.OpenBiblePublicationTrackSelectionModalAsync(trackSelectionViewModel);

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

