#nullable enable
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
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
    private readonly IToastService toastService;
    private BibleReadingSchedule? current;
    private BibleReadingSchedule? tentative;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IDownloadService downloadService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;

    private NotifyCollectionChangedEventHandler? collectionChangedHandler;

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
        this.toastService = toastService;
        this.downloadService = downloadService;
        this.urlRefreshService = urlRefreshService;
        this.mapper = mapper;

        this.state = state;
        this.dispatcher = dispatcher;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopAsync();
            IsBusy = false;
        });

        SetChapterCommand = new RelayCommand<BibleChapterListViewItemModel>(x =>
        {
            if (x is null)
            {
                return;
            }

            IsBusy = true;

            SelectedChapter?.IsSelected = false;

            SelectedChapter = x;
            SelectedChapter.IsSelected = true;

            tentative?.ChapterNumber = x.Number;

            if (tentative is null)
            {
                return;
            }

            // Map entity to DTO before dispatching
            var chapterSelectedItem = new BibleReadingStateItem
            {
                LanguageCode = tentative.LanguageCode,
                PublicationCode = tentative.PublicationCode,
                BookNumber = tentative.BookNumber,
                ChapterNumber = x.Number
            };
            dispatcher.Dispatch(new ChapterSelectedAction(chapterSelectedItem));
            IsBusy = false;
        });

        state.StateChanged += OnBibleReadingInitialized;
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;
        if (stateValue.CurrentBibleReadingSchedule is null || stateValue.TentativeBibleReadingSchedule is null)
        {
            return;
        }
        // Map DTOs to entities
        current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        tentative = mapper.Map<BibleReadingSchedule>(stateValue.TentativeBibleReadingSchedule);
        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(tentative.LanguageCode, tentative.PublicationCode, tentative.BookNumber);

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following book selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    public ICommand BackCommand { get; set; }
    public ICommand SetChapterCommand { get; set; }

    public BibleChapterListViewItemModel? SelectedChapter { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<BibleChapterListViewItemModel>? chapters;

    public ObservableCollection<BibleChapterListViewItemModel> Chapters
    {
        get => chapters ??= [];
        set => SetProperty(ref chapters, value);
    }

    private readonly SemaphoreSlim @lock = new(1);

    private async Task Initialize(string languageCode, string publicationCode, int bookNumber)
    {
        await PopulateChapters(languageCode, publicationCode, bookNumber);
    }


    private async Task PopulateChapters(string languageCode, string publicationCode, int bookNumber)
    {
        // Run database operations off UI thread
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber));

        // Build the list of chapter view models
        var chapterViewModelList = new List<BibleChapterListViewItemModel>();
        BibleChapterListViewItemModel? selectedChapter = null;

        foreach (var chapter in chapters.Select(x => x.Value))
        {
            var chapterVm = new BibleChapterListViewItemModel(chapter);

            chapterViewModelList.Add(chapterVm);

            if (current is null || tentative is null)
            {
                continue;
            }

            if (current.LanguageCode != tentative.LanguageCode
                || current.PublicationCode != tentative.PublicationCode
                || current.BookNumber != tentative.BookNumber
                || current.ChapterNumber != chapter.Number)
            {
                continue;
            }

            selectedChapter = chapterVm;
            selectedChapter.IsSelected = true;
        }

        // Assign the complete collection on main thread to ensure CollectionView refreshes
        // CollectionView responds better to property change notifications than collection modification
        // Follow the same pattern as language modal: assign collection but don't set IsBusy here
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Chapters = new ObservableCollection<BibleChapterListViewItemModel>(chapterViewModelList);

            if (selectedChapter is not null)
            {
                SelectedChapter = selectedChapter;
            }
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnBibleReadingInitialized;

        // Unsubscribe from collection changes
        if (Chapters is not null && collectionChangedHandler is not null)
        {
            Chapters.CollectionChanged -= collectionChangedHandler;
        }

        propertyChangedHandlers.Clear();

        @lock.Dispose();

        GC.SuppressFinalize(this);
    }
}

public sealed class BibleChapterListViewItemModel : ObservableObject, IComparable
{
    private readonly BibleChapter chapter;

    public BibleChapterListViewItemModel(BibleChapter chapter)
    {
        this.chapter = chapter;
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string LookUpPath => chapter.Source?.LookUpPath ?? string.Empty;
    public int Number => chapter.Number;

    public string Title => chapter.Title;
    public string Url => chapter.Source?.Url ?? string.Empty;

    public int CompareTo(object? obj) => Number.CompareTo((obj as BibleChapterListViewItemModel)?.Number);
}
