using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Bible;

public class ChapterSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;

    private readonly MediaService _mediaService;
    private readonly IToastService _toastService;
    private readonly IPreviewPlayService _playService;
    private BibleReadingSchedule _current;
    private BibleReadingSchedule _tentative;
    private readonly INavigation _navigation;
    private readonly IMediaCacheService _cacheService;
    private readonly IDownloadService _downloadService;
    private readonly Fluxor.IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    private readonly List<IDisposable> _subscriptions = [];
    private readonly Dictionary<BibleChapterListViewItemModel, PropertyChangedEventHandler> _propertyChangedHandlers = [];

    public ChapterSelectionViewModel(
        ILogger logger,
        MediaService mediaService,
        IToastService toastService,
        IPreviewPlayService playService,
        INavigation navigation,
        IDownloadService downloadService,
        IMediaCacheService cacheService,
        Fluxor.IDispatcher dispatcher,
        IState<ApplicationState> state)
    {
        _logger = logger;
        _mediaService = mediaService;
        _toastService = toastService;
        _playService = playService;
        _navigation = navigation;
        _downloadService = downloadService;
        _cacheService = cacheService;
        _dispatcher = dispatcher;
        _state = state;

        BackCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigation.PopAsync();
            IsBusy = false;
        });

        SetChapterCommand = new Command<BibleChapterListViewItemModel>(x =>
        {
            IsBusy = true;

            if (SelectedChapter != null) SelectedChapter.IsSelected = false;

            SelectedChapter = x;
            SelectedChapter.IsSelected = true;

            _tentative.ChapterNumber = x.Number;

            _dispatcher.Dispatch(new ChapterSelectedAction(new BibleReadingSchedule
            {
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                BookNumber = _tentative.BookNumber,
                ChapterNumber = x.Number
            }));
            IsBusy = false;
        });

        //set schedules from initial state.
        //this should fire only once 
        EventHandler subscriptionHandler = null;
        subscriptionHandler = (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentBibleReadingSchedule != null && state.TentativeBibleReadingSchedule != null)
            {
                _current = state.CurrentBibleReadingSchedule;
                _tentative = state.TentativeBibleReadingSchedule;
                Task.Run(async () =>
                {
                    await Initialize(_tentative.LanguageCode, _tentative.PublicationCode, _tentative.BookNumber);
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                });
                _state.StateChanged -= subscriptionHandler;
            }
        };
        _state.StateChanged += subscriptionHandler;
    }

    public ICommand BackCommand { get; set; }
    public ICommand SetChapterCommand { get; set; }

    public BibleChapterListViewItemModel SelectedChapter { get; set; }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private ObservableCollection<BibleChapterListViewItemModel> _chapters;

    public ObservableCollection<BibleChapterListViewItemModel> Chapters
    {
        get => _chapters;
        set => SetProperty(ref _chapters, value);
    }

    private BibleChapterListViewItemModel _currentlyPlaying;
    private readonly SemaphoreSlim _lock = new(1);

    private async Task Initialize(string languageCode, string publicationCode, int bookNumber)
    {
        await PopulateChapters(languageCode, publicationCode, bookNumber);

        // Subscribe to PropertyChanged events for Play/Stop
        foreach (var chapter in Chapters)
        {
            SubscribeToChapterEvents(chapter);
        }

        // Subscribe to collection changes to handle new items
        Chapters.CollectionChanged += (sender, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (BibleChapterListViewItemModel item in e.NewItems)
                {
                    SubscribeToChapterEvents(item);
                }
            }
            if (e.OldItems != null)
            {
                foreach (BibleChapterListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromChapterEvents(item);
                }
            }
        };

        // Subscribe to play service stopped event
        _playService.OnStopped += OnPlayServiceStopped;
    }

    private void SubscribeToChapterEvents(BibleChapterListViewItemModel chapter)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (e.PropertyName == "Play" && sender is BibleChapterListViewItemModel item)
            {
                if (item.Play)
                {
                    _ = HandlePlayChapter(item);
                }
                else
                {
                    _playService.Stop();
                }
            }
        };

        chapter.PropertyChanged += handler;
        _propertyChangedHandlers[chapter] = handler;
    }

    private void UnsubscribeFromChapterEvents(BibleChapterListViewItemModel chapter)
    {
        if (_propertyChangedHandlers.TryGetValue(chapter, out var handler))
        {
            chapter.PropertyChanged -= handler;
            _propertyChangedHandlers.Remove(chapter);
        }
    }

    private async Task HandlePlayChapter(BibleChapterListViewItemModel chapter)
    {
        await _lock.WaitAsync();

        try
        {
            if (_currentlyPlaying != null && _currentlyPlaying != chapter)
            {
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
            }

            _currentlyPlaying = chapter;
            _currentlyPlaying.IsBusy = true;

            try
            {
                var url = chapter.Url;

                await Task.Run(async () =>
                {
                    if (!await _downloadService.FileExists(url))
                        url = await _cacheService.GetBibleChapterUrl(
                            _tentative.LanguageCode,
                            _tentative.PublicationCode,
                            _tentative.BookNumber,
                            chapter.Number,
                            chapter.LookUpPath);

                    await _playService.Play(url);
                });
            }
            catch
            {
                _currentlyPlaying.Play = false;
                await _toastService.ShowMessage("Failed to download the file.");
            }

            _currentlyPlaying.IsBusy = false;
        }
        finally
        {
            try
            {
                _lock.Release();
            }
            catch (ObjectDisposedException e)
            {
                _logger.Error(e, "ChapterSelectionViewModel: @lock disposed error.");
            }
        }
    }

    private async void OnPlayServiceStopped()
    {
        await _lock.WaitAsync();

        try
        {
            if (_currentlyPlaying != null)
            {
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
            }
        }
        finally
        {
            try
            {
                _lock.Release();
            }
            catch (ObjectDisposedException e)
            {
                _logger.Error(e, "ChapterSelectionViewModel: @lock disposed error.");
            }
        }
    }

    private async Task PopulateChapters(string languageCode, string publicationCode, int bookNumber)
    {
        var chapters = await _mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
        var chapterVMs = new ObservableCollection<BibleChapterListViewItemModel>();

        if (DeviceInfo.Platform == DevicePlatform.WinUI) Chapters = chapterVMs;

        foreach (var chapter in chapters.Select(x => x.Value))
        {
            var chapterVm = new BibleChapterListViewItemModel(chapter);

            chapterVMs.Add(chapterVm);

            if (_current.LanguageCode == _tentative.LanguageCode
                && _current.PublicationCode == _tentative.PublicationCode
                && _current.BookNumber == _tentative.BookNumber
                && _current.ChapterNumber == chapter.Number)
            {
                chapterVm.IsSelected = true;
                SelectedChapter = chapterVm;
            }
        }

        if (DeviceInfo.Platform != DevicePlatform.WinUI) Chapters = chapterVMs;
    }

    public void Dispose()
    {
        _playService.OnStopped -= OnPlayServiceStopped;
        
        foreach (var chapter in Chapters)
        {
            UnsubscribeFromChapterEvents(chapter);
        }
        
        _subscriptions.ForEach(x => x.Dispose());
        _propertyChangedHandlers.Clear();

        _lock.Dispose();
        
        // Note: _mediaService (MediaService), _toastService (IToastService), 
        // _playService (IPreviewPlayService), _downloadService (IDownloadService), 
        // and _cacheService (IMediaCacheService) are singletons and should not be 
        // disposed here as they are managed by the DI container
    }
}

public class BibleChapterListViewItemModel : ObservableObject, IComparable
{
    private readonly BibleChapter _chapter;

    public BibleChapterListViewItemModel(BibleChapter chapter)
    {
        _chapter = chapter;
        TogglePlayCommand = new Command(() => Play = !Play);
    }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand TogglePlayCommand { get; set; }

    public string LookUpPath => _chapter.Source.LookUpPath;
    public int Number => _chapter.Number;

    public string Title => _chapter.Title;
    public string Url => _chapter.Source.Url;

    private bool _play;

    public bool Play
    {
        get => _play;
        set => SetProperty(ref _play, value);
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public int CompareTo(object obj)
    {
        return Number.CompareTo((obj as BibleChapterListViewItemModel).Number);
    }
}