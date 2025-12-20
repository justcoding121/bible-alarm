#nullable enable
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
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

public partial class ChapterSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;

    private readonly IMediaService mediaService;
    private readonly IToastService toastService;
    private readonly IAudioPreviewer playService;
    private BibleReadingSchedule? current;
    private BibleReadingSchedule? tentative;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IDownloadService downloadService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private bool initComplete;

    private readonly Dictionary<BibleChapterListViewItemModel, PropertyChangedEventHandler> propertyChangedHandlers = [];
    private NotifyCollectionChangedEventHandler? collectionChangedHandler;

    public ChapterSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        IAudioPreviewer playService,
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
        this.playService = playService;
        this.downloadService = downloadService;
        this.urlRefreshService = urlRefreshService;
        this.mapper = mapper;

        this.state = state;
        this.dispatcher = dispatcher;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            // Stop any ongoing preview playback before navigating away
            playService.Stop();
            if (currentlyPlaying is not null)
            {
                currentlyPlaying.Play = false;
                currentlyPlaying.IsBusy = false;
                currentlyPlaying = null;
            }

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

    private BibleChapterListViewItemModel? currentlyPlaying;
    private readonly SemaphoreSlim @lock = new(1);

    private async Task Initialize(string languageCode, string publicationCode, int bookNumber)
    {
        await PopulateChapters(languageCode, publicationCode, bookNumber);

        // Subscribe to PropertyChanged events for Play/Stop
        foreach (var chapter in Chapters)
        {
            SubscribeToChapterEvents(chapter);
        }

        // Subscribe to collection changes to handle new items
        collectionChangedHandler = (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (BibleChapterListViewItemModel item in e.NewItems)
                {
                    SubscribeToChapterEvents(item);
                }
            }

            if (e.OldItems is null)
            {
                return;
            }

            {
                foreach (BibleChapterListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromChapterEvents(item);
                }
            }
        };
        Chapters.CollectionChanged += collectionChangedHandler;

        // Subscribe to play service stopped event
        playService.OnStopped += OnPlayServiceStopped;
    }

    private void SubscribeToChapterEvents(BibleChapterListViewItemModel chapter)
    {
        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "Play" || sender is not BibleChapterListViewItemModel item)
            {
                return;
            }

            if (item.Play)
            {
                _ = HandlePlayChapter(item);
            }
            else
            {
                playService.Stop();
            }
        }

        chapter.PropertyChanged += Handler;
        propertyChangedHandlers[chapter] = Handler;
    }

    private void UnsubscribeFromChapterEvents(BibleChapterListViewItemModel chapter)
    {
        if (!propertyChangedHandlers.TryGetValue(chapter, out var handler))
        {
            return;
        }

        chapter.PropertyChanged -= handler;
        propertyChangedHandlers.Remove(chapter);
    }

    private async Task HandlePlayChapter(BibleChapterListViewItemModel chapter)
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (currentlyPlaying is not null && currentlyPlaying != chapter)
            {
                currentlyPlaying.Play = false;
                currentlyPlaying.IsBusy = false;
            }

            currentlyPlaying = chapter;
            currentlyPlaying.IsBusy = true;

            try
            {
                var url = chapter.Url;

                await Task.Run(async () =>
                {
                    if (!await downloadService.FileExists(url))
                    {
                        if (tentative is null)
                        {
                            return;
                        }

                        url = await urlRefreshService.GetBibleChapterUrl(
                            tentative.LanguageCode,
                            tentative.PublicationCode,
                            tentative.BookNumber,
                            chapter.Number,
                            chapter.LookUpPath);
                    }

                    await playService.Play(url);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error playing chapter preview");
                currentlyPlaying?.Play = false;
                await toastService.ShowMessage("Media download failed. Check your internet connection.");
            }
            finally
            {
                currentlyPlaying?.IsBusy = false;
            }
        }, ex => logger.Error(ex, "ChapterSelectionViewModel: @lock disposed error."));
    }

    private async void OnPlayServiceStopped()
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(@lock, () =>
            {
                if (currentlyPlaying is null)
                {
                    return Task.CompletedTask;
                }

                currentlyPlaying.Play = false;
                currentlyPlaying.IsBusy = false;
                currentlyPlaying = null;
                return Task.CompletedTask;
            }, ex => logger.Error(ex, "ChapterSelectionViewModel: @lock disposed error."));
        }
        catch (Exception e)
        {
            logger.Error(e, "ChapterSelectionViewModel: OnPlayServiceStopped error.");
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
        playService.OnStopped -= OnPlayServiceStopped;

        // Unsubscribe from collection changes
        if (Chapters is not null && collectionChangedHandler is not null)
        {
            Chapters.CollectionChanged -= collectionChangedHandler;
        }

        // Stop any ongoing preview playback when navigating away
        playService.Stop();

        if (Chapters is not null)
        {
            foreach (var chapter in Chapters)
            {
                UnsubscribeFromChapterEvents(chapter);
            }
        }

        propertyChangedHandlers.Clear();

        @lock.Dispose();

        GC.SuppressFinalize(this);
    }
}

public partial class BibleChapterListViewItemModel : ObservableObject, IComparable
{
    private readonly BibleChapter chapter;

    public BibleChapterListViewItemModel(BibleChapter chapter)
    {
        this.chapter = chapter;
        TogglePlayCommand = new RelayCommand(() => Play = !Play);
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public ICommand TogglePlayCommand { get; set; }

    public string LookUpPath => chapter.Source?.LookUpPath ?? string.Empty;
    public int Number => chapter.Number;

    public string Title => chapter.Title;
    public string Url => chapter.Source?.Url ?? string.Empty;

    private bool play;

    public bool Play
    {
        get => play;
        set => SetProperty(ref play, value);
    }

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public int CompareTo(object? obj)
    {
        return Number.CompareTo((obj as BibleChapterListViewItemModel)?.Number);
    }
}
