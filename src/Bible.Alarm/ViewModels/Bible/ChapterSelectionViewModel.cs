#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Bible.ChapterSelectionViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public sealed class ChapterSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    // Helper classes
    private readonly ChapterSelectionStateManager stateManager;
    private readonly ChapterSelectionDataProvider dataProvider;
    private readonly ChapterSelectionCommandHandler commandHandler;
    private readonly ChapterSelectionPropertyManager propertyManager;

    private readonly SemaphoreSlim @lock = new(1);

    public ChapterSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Initialize helper classes
        stateManager = new ChapterSelectionStateManager(mapper);
        dataProvider = new ChapterSelectionDataProvider(mediaService);
        commandHandler = new ChapterSelectionCommandHandler(logger, state, dispatcher, navigationService);
        propertyManager = new ChapterSelectionPropertyManager();

        BackCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        SetChapterCommand = new AsyncRelayCommand<BibleChapterListViewItemModel>(async x =>
        {
            if (x != null)
            {
                propertyManager.SelectedChapter?.IsSelected = false;
                propertyManager.SelectedChapter = x;
                propertyManager.SelectedChapter.IsSelected = true;
                await commandHandler.HandleSetChapterAsync(x);
            }
        });

        state.StateChanged += OnBibleReadingInitialized;
        state.StateChanged += OnBibleReadingChanged;
    }

    private void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        stateManager.HandleBibleReadingChanged(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub, book) => await Initialize(lang, pub, book),
            SetSelectedChapter);
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        stateManager.HandleBibleReadingInitialized(
            state,
            busy => propertyManager.IsBusy = busy,
            async (lang, pub, book) => await Initialize(lang, pub, book));
    }

    /// <summary>
    /// Refreshes the ViewModel from the latest state when the modal appears.
    /// This ensures chapters are populated and current is initialized from CurrentSchedule.
    /// </summary>
    public async Task RefreshFromState()
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;
        var newBookNumber = currentSchedule.BibleReadingBookNumber;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode) || !newBookNumber.HasValue)
        {
            return;
        }

        stateManager.UpdateFromState(state, mapper);

        // Ensure chapters are populated if not already initialized
        if (!stateManager.InitComplete || propertyManager.Chapters == null || propertyManager.Chapters.Count == 0)
        {
            stateManager.SetInitComplete(true);
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = true);
            await Initialize(newLanguageCode, newPublicationCode, newBookNumber.Value);
            // Set selected chapter after chapters are populated
            SetSelectedChapter();
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.IsBusy = false);
        }
        else
        {
            // Update selected chapter when state changes (e.g., after navigating back)
            SetSelectedChapter();
        }
    }

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
    public ICommand SetChapterCommand { get; set; }

    public BibleChapterListViewItemModel? SelectedChapter
    {
        get => propertyManager.SelectedChapter;
        set => propertyManager.SelectedChapter = value;
    }

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public ObservableCollection<BibleChapterListViewItemModel> Chapters
    {
        get => propertyManager.Chapters;
        set => propertyManager.Chapters = value;
    }

    private async Task Initialize(string languageCode, string publicationCode, int bookNumber)
    {
        await dataProvider.PopulateChapters(
            languageCode,
            publicationCode,
            bookNumber,
            stateManager.Current,
            propertyManager.Chapters,
            chapter => propertyManager.SelectedChapter = chapter);
    }

    private void SetSelectedChapter()
    {
        dataProvider.SetSelectedChapter(
            stateManager.Current,
            propertyManager.Chapters,
            propertyManager.SelectedChapter,
            chapter => propertyManager.SelectedChapter = chapter);
    }

    public void Dispose()
    {
        state.StateChanged -= OnBibleReadingInitialized;
        state.StateChanged -= OnBibleReadingChanged;
        @lock.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class BibleChapterListViewItemModel : ObservableObject, IComparable
{
    private readonly BiblePublicationChapter chapter;

    public BibleChapterListViewItemModel(BiblePublicationChapter chapter)
    {
        this.chapter = chapter;
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    // LookUpPath is no longer stored in the database - it's computed at runtime by TrackMetadata
    public int Number => chapter.Number;

    public string Title => chapter.Title;
    public string Url => chapter.Source?.Url ?? string.Empty;

    public int CompareTo(object? obj) => Number.CompareTo((obj as BibleChapterListViewItemModel)?.Number);
}
