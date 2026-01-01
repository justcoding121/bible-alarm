#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

/// <summary>
/// Handles initialization of commands for bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BibleCommandInitializer
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IScheduleSelectionService scheduleSelectionService;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly IServiceProvider serviceProvider;

    public BibleCommandInitializer(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IDispatcher dispatcher,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.scheduleSelectionService = scheduleSelectionService;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.serviceProvider = serviceProvider;
    }

    public ICommand CreateSelectLanguageCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            logger.Information("BibleSelectionContainerViewModel: SelectLanguageCommand - Opening language modal");
            // Create a temporary BibleSelectionViewModel instance for the language modal
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BibleSelectionViewModel>();
            logger.Debug("BibleSelectionContainerViewModel: SelectLanguageCommand - Created BibleSelectionViewModel, opening modal");
            await navigationService.OpenLanguageModalAsync(bibleSelectionViewModel);
            logger.Debug("BibleSelectionContainerViewModel: SelectLanguageCommand - Modal opened");
        });
    }

    public ICommand CreateSelectBibleCommand(Func<BibleReadingSchedule?> getBibleReading, Action<BibleReadingSchedule?> setBibleReading, int scheduleId, bool isNewSchedule, bool bibleReadingUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedBibleReading = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, getBibleReading()));
            setBibleReading(loadedBibleReading);

            // Create view model and open modal
            var bibleSelectionViewModel = serviceProvider.GetRequiredService<BibleSelectionViewModel>();
            await navigationService.OpenBibleSelectionModalAsync(bibleSelectionViewModel);

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = loadedBibleReading != null
                ? mapper.Map<BibleReadingStateItem>(loadedBibleReading)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BibleSelectionAction(currentBibleReadingItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }

    public ICommand CreateSelectBookCommand(Func<BibleReadingSchedule?> getBibleReading, Action<BibleReadingSchedule?> setBibleReading, int scheduleId, bool isNewSchedule, bool bibleReadingUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedBibleReading = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, getBibleReading()));
            setBibleReading(loadedBibleReading);

            // Create view model and open modal
            var bookSelectionViewModel = serviceProvider.GetRequiredService<BookSelectionViewModel>();
            await navigationService.OpenBookSelectionModalAsync(bookSelectionViewModel);

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = loadedBibleReading != null
                ? mapper.Map<BibleReadingStateItem>(loadedBibleReading)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BookSelectionAction(currentBibleReadingItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }

    public ICommand CreateSelectChapterCommand(Func<BibleReadingSchedule?> getBibleReading, Action<BibleReadingSchedule?> setBibleReading, int scheduleId, bool isNewSchedule, bool bibleReadingUpdated)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            var loadedBibleReading = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, getBibleReading()));
            setBibleReading(loadedBibleReading);

            // Create view model and open modal
            var chapterSelectionViewModel = serviceProvider.GetRequiredService<ChapterSelectionViewModel>();
            await navigationService.OpenChapterSelectionModalAsync(chapterSelectionViewModel);

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = loadedBibleReading != null
                ? mapper.Map<BibleReadingStateItem>(loadedBibleReading)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new ChapterSelectionAction(currentBibleReadingItem));
                // State change will trigger OnStateChanged which handles cascading notifications
            }
        });
    }
}

