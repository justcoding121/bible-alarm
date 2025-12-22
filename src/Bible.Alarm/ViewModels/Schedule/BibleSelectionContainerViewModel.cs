using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class BibleSelectionContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IScheduleSelectionService scheduleSelectionService;
    private readonly IBibleNavigationService bibleNavigationService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    private int scheduleId;
    private bool isNewSchedule;
    private bool bibleReadingUpdated;
    private BibleReadingSchedule? bibleReadingSchedule;
    private AlarmSchedule? model;

    public BibleSelectionContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IScheduleSelectionService scheduleSelectionService,
        IBibleNavigationService bibleNavigationService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.scheduleSelectionService = scheduleSelectionService;
        this.bibleNavigationService = bibleNavigationService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        state.StateChanged += OnStateChanged;
        InitializeCommands();
        InitializeFromState();
    }

    private void InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            scheduleId = currentSchedule.Id;
            isNewSchedule = currentSchedule.Id <= 0;
            
            OnPropertyChanged(nameof(TranslationDisplayText));
            OnPropertyChanged(nameof(BookDisplayText));
            OnPropertyChanged(nameof(ChapterDisplayText));
        }
    }

    private void InitializeCommands()
    {
        SelectBibleCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            bibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, bibleReadingSchedule));

            if (bibleReadingSchedule != null)
            {
                OnPropertyChanged(nameof(TranslationDisplayText));
                OnPropertyChanged(nameof(BookDisplayText));
                OnPropertyChanged(nameof(ChapterDisplayText));
            }

            await navigationService.NavigateToBibleSelectionAsync();

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = bibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(bibleReadingSchedule)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BibleSelectionAction(currentBibleReadingItem));
            }
        });

        SelectBookCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            bibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, bibleReadingSchedule));

            if (bibleReadingSchedule != null)
            {
                OnPropertyChanged(nameof(TranslationDisplayText));
                OnPropertyChanged(nameof(BookDisplayText));
                OnPropertyChanged(nameof(ChapterDisplayText));
            }

            await navigationService.NavigateToBookSelectionAsync();

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = bibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(bibleReadingSchedule)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new BookSelectionAction(currentBibleReadingItem));
            }
        });

        SelectChapterCommand = new AsyncRelayCommand(async () =>
        {
            // Run database operations off UI thread
            bibleReadingSchedule = await Task.Run(async () =>
                await scheduleSelectionService.LoadBibleReadingForSelectionAsync(
                    scheduleId, isNewSchedule, bibleReadingUpdated, bibleReadingSchedule));

            if (bibleReadingSchedule != null)
            {
                OnPropertyChanged(nameof(TranslationDisplayText));
                OnPropertyChanged(nameof(BookDisplayText));
                OnPropertyChanged(nameof(ChapterDisplayText));
            }

            await navigationService.NavigateToChapterSelectionAsync();

            // Map entities to DTOs before dispatching
            var currentBibleReadingItem = bibleReadingSchedule != null
                ? mapper.Map<BibleReadingStateItem>(bibleReadingSchedule)
                : null;

            if (currentBibleReadingItem != null)
            {
                dispatcher.Dispatch(new ChapterSelectionAction(currentBibleReadingItem));
            }
        });

        PreviousBookCommand = new AsyncRelayCommand(async () =>
        {
            if (bibleReadingSchedule == null || model == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService.MoveToPreviousBookAsync(bibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                // Update the model to reflect changes
                model.BibleReadingSchedule = bibleReadingSchedule;
                
                // Create state item and dispatch update action to save to DB and update state
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, true));
                
                // Notify UI of changes
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(BookDisplayText));
                    OnPropertyChanged(nameof(ChapterDisplayText));
                });
            }
        });

        NextBookCommand = new AsyncRelayCommand(async () =>
        {
            if (bibleReadingSchedule == null || model == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService.MoveToNextBookAsync(bibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                // Update the model to reflect changes
                model.BibleReadingSchedule = bibleReadingSchedule;
                
                // Create state item and dispatch update action to save to DB and update state
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, true));
                
                // Notify UI of changes
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(BookDisplayText));
                    OnPropertyChanged(nameof(ChapterDisplayText));
                });
            }
        });

        PreviousChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (bibleReadingSchedule == null || model == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService.MoveToPreviousChapterAsync(bibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                // Update the model to reflect changes
                model.BibleReadingSchedule = bibleReadingSchedule;
                
                // Create state item and dispatch update action to save to DB and update state
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, true));
                
                // Notify UI of changes (book might change if crossing book boundaries)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(BookDisplayText));
                    OnPropertyChanged(nameof(ChapterDisplayText));
                });
            }
        });

        NextChapterCommand = new AsyncRelayCommand(async () =>
        {
            if (bibleReadingSchedule == null || model == null)
            {
                return;
            }

            // Run database operations off UI thread
            var moved = await Task.Run(async () =>
                await bibleNavigationService.MoveToNextChapterAsync(bibleReadingSchedule));

            if (moved)
            {
                bibleReadingUpdated = true;
                // Update the model to reflect changes
                model.BibleReadingSchedule = bibleReadingSchedule;
                
                // Create state item and dispatch update action to save to DB and update state
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, true));
                
                // Notify UI of changes (book might change if crossing book boundaries)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(BookDisplayText));
                    OnPropertyChanged(nameof(ChapterDisplayText));
                });
            }
        });
    }

    public void SetModel(AlarmSchedule model)
    {
        this.model = model;
    }

    public void SetScheduleId(int scheduleId, bool isNewSchedule)
    {
        this.scheduleId = scheduleId;
        this.isNewSchedule = isNewSchedule;
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            // Initialize if schedule ID changed
            if (currentSchedule.Id != scheduleId)
            {
                InitializeFromState();
            }
            
            if (model != null)
            {
                // Update bibleReadingUpdated flag if bible reading changed
                var hasBibleReading = currentSchedule.BibleReadingScheduleId.HasValue;
                if (hasBibleReading && (bibleReadingSchedule == null || 
                    bibleReadingSchedule.Id != currentSchedule.BibleReadingScheduleId.Value))
                {
                    bibleReadingUpdated = true;
                }
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(TranslationDisplayText));
            OnPropertyChanged(nameof(BookDisplayText));
            OnPropertyChanged(nameof(ChapterDisplayText));
        });
    }

    public ICommand SelectBibleCommand { get; private set; }
    public ICommand SelectBookCommand { get; private set; }
    public ICommand SelectChapterCommand { get; private set; }
    public ICommand PreviousBookCommand { get; private set; }
    public ICommand NextBookCommand { get; private set; }
    public ICommand PreviousChapterCommand { get; private set; }
    public ICommand NextChapterCommand { get; private set; }

    public string TranslationDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return string.Empty;
            }
            
            string publicationCode = currentSchedule.BibleReadingPublicationCode?.ToLowerInvariant() ?? string.Empty;
            string languageName = currentSchedule.BibleReadingLanguageName ?? string.Empty;
            
            if (string.IsNullOrEmpty(publicationCode) && string.IsNullOrEmpty(languageName))
            {
                return string.Empty;
            }

            // Format publication code using helper
            var publicationDisplay = PublicationDisplayHelper.GetDisplayName(publicationCode);

            if (string.IsNullOrEmpty(languageName))
            {
                return publicationDisplay;
            }

            if (string.IsNullOrEmpty(publicationCode))
            {
                return languageName;
            }

            // Format: "NWT 2013 - English"
            return $"{publicationDisplay} - {languageName}";
        }
    }

    public string BookDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return string.Empty;
            }

            // Get book name from state (populated during bootstrap)
            return currentSchedule.BibleReadingBookName ?? string.Empty;
        }
    }

    public string ChapterDisplayText
    {
        get
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null || !currentSchedule.BibleReadingScheduleId.HasValue || !currentSchedule.BibleReadingChapterNumber.HasValue)
            {
                return string.Empty;
            }
            
            int chapterNumber = currentSchedule.BibleReadingChapterNumber.Value;
            
            if (chapterNumber <= 0)
            {
                return string.Empty;
            }

            // ChapterNumber is always valid (1-150) if BibleReadingSchedule exists
            return $"Chapter {chapterNumber}";
        }
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}

