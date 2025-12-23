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
using Bible.Alarm.Stores.Actions.Schedule;
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
    private BibleReadingSchedule? lastCurrent;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IDownloadService downloadService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;
    
    // Track last language, publication code, and book number to detect changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private int? lastBookNumber;

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

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopModalAsync();
            IsBusy = false;
        });

        SetChapterCommand = new AsyncRelayCommand<BibleChapterListViewItemModel>(async x =>
        {
            if (x is null)
            {
                return;
            }

            IsBusy = true;

            try
            {
                SelectedChapter?.IsSelected = false;

                SelectedChapter = x;
                SelectedChapter.IsSelected = true;

                if (current is null)
                {
                    return;
                }

                // Map entity to DTO before dispatching
                // IMPORTANT: Include display names from current state (no database query needed)
                // Get display names from CurrentSchedule (they should already be populated)
                var currentSchedule = state.Value.CurrentSchedule;
                var chapterSelectedItem = new BibleReadingStateItem
                {
                    LanguageCode = current.LanguageCode,
                    PublicationCode = current.PublicationCode,
                    BookNumber = current.BookNumber,
                    ChapterNumber = x.Number,
                    // Store display names from current state
                    LanguageName = currentSchedule?.BibleReadingLanguageName,
                    PublicationName = currentSchedule?.BibleReadingPublicationName,
                    BookName = currentSchedule?.BibleReadingBookName
                };
                dispatcher.Dispatch(new ChapterSelectedAction(chapterSelectedItem));

                // Navigate back to schedule page
                await navigationService.PopModalAsync();
            }
            finally
            {
                IsBusy = false;
            }
        });

        state.StateChanged += OnBibleReadingInitialized;
        state.StateChanged += OnBibleReadingChanged;
    }

    private void OnBibleReadingChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        
        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
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

        // Check if language, publication code, or book number changed (need to repopulate chapters)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var bookNumberChanged = lastBookNumber != newBookNumber.Value;
        var needsRepopulation = languageChanged || publicationCodeChanged || bookNumberChanged;
        
        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastBookNumber = newBookNumber.Value;
        
        // Update current if we have CurrentBibleReadingSchedule (for other properties like ChapterNumber)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                BookNumber = newBookNumber.Value,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            lastCurrent = current;
        }

        // If language, publication code, or book number changed, repopulate chapters
        if (needsRepopulation && initComplete)
        {
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                await Initialize(newLanguageCode, newPublicationCode, newBookNumber.Value);
                await Task.Delay(100); // Give CollectionView time to render
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });
        }
        else
        {
            // Update selected chapter when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(SetSelectedChapter);
        }
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;
        
        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
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

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastBookNumber = newBookNumber.Value;
        
        // Update current if we have CurrentBibleReadingSchedule (for other properties like ChapterNumber)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                BookNumber = newBookNumber.Value,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            lastCurrent = current;
        }
        
        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(newLanguageCode, newPublicationCode, newBookNumber.Value);

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following book selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    public ICommand BackCommand { get; set; }
    public ICommand CloseModalCommand { get; set; }
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

    private void SetSelectedChapter()
    {
        if (current == null || Chapters == null || Chapters.Count == 0)
        {
            return;
        }

        if (SelectedChapter != null)
        {
            SelectedChapter.IsSelected = false;
        }

        var chapter = Chapters.FirstOrDefault(c => c.Number == current.ChapterNumber);
        if (chapter != null)
        {
            SelectedChapter = chapter;
            SelectedChapter.IsSelected = true;
        }
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

            if (current is null)
            {
                continue;
            }

            if (current.ChapterNumber != chapter.Number)
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
        state.StateChanged -= OnBibleReadingChanged;

        // Unsubscribe from collection changes
        if (Chapters is not null && collectionChangedHandler is not null)
        {
            Chapters.CollectionChanged -= collectionChangedHandler;
        }

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
