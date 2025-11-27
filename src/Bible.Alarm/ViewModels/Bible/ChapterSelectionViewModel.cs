#nullable enable
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.Essentials;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible;

public partial class ChapterSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;

    private readonly MediaService _mediaService;
    private readonly IToastService _toastService;
    private readonly IAudioPreviewer _playService;
    private BibleReadingSchedule? _current;
    private BibleReadingSchedule? _tentative;
    private readonly IMediaUrlRefreshService _urlRefreshService;
    private readonly IDownloadService _downloadService;
    private readonly IState<ApplicationState> _state;
    private readonly IDispatcher _dispatcher;
    private bool _initComplete;

    private readonly Dictionary<BibleChapterListViewItemModel, PropertyChangedEventHandler> _propertyChangedHandlers = [];
    private NotifyCollectionChangedEventHandler? _collectionChangedHandler;

    public ChapterSelectionViewModel(
        ILogger logger,
        MediaService mediaService,
        IToastService toastService,
        IAudioPreviewer playService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _mediaService = mediaService;
        _toastService = toastService;
        _playService = playService;
        _downloadService = downloadService;
        _urlRefreshService = urlRefreshService;

        _state = state;
        _dispatcher = dispatcher;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            
            // Stop any ongoing preview playback before navigating away
            _playService.Stop();
            if (_currentlyPlaying is not null)
            {
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
            }
            
            await navigationService.PopAsync();
            IsBusy = false;
        });

        SetChapterCommand = new RelayCommand<BibleChapterListViewItemModel>(x =>
        {
            if (x is null) return;
            
            IsBusy = true;

            SelectedChapter?.IsSelected = false;

            SelectedChapter = x;
            SelectedChapter.IsSelected = true;

            _tentative?.ChapterNumber = x.Number;

            if (_tentative is null) return;

            _dispatcher.Dispatch(new ChapterSelectedAction(new BibleReadingSchedule
            {
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                BookNumber = _tentative.BookNumber,
                ChapterNumber = x.Number
            }));
            IsBusy = false;
        });

        _state.StateChanged += OnBibleReadingInitialized;
    }

    private void OnBibleReadingInitialized(object? o, EventArgs eventArgs)
    {
        if (_initComplete) return;
        var stateValue = _state.Value;
        if (stateValue.CurrentBibleReadingSchedule is null || stateValue.TentativeBibleReadingSchedule is null) return;
        _current = stateValue.CurrentBibleReadingSchedule;
        _tentative = stateValue.TentativeBibleReadingSchedule;
        _initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(_tentative.LanguageCode, _tentative.PublicationCode, _tentative.BookNumber);
            
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

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private ObservableCollection<BibleChapterListViewItemModel>? _chapters;

    public ObservableCollection<BibleChapterListViewItemModel> Chapters
    {
        get => _chapters ??= [];
        set => SetProperty(ref _chapters, value);
    }

    private BibleChapterListViewItemModel? _currentlyPlaying;
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
        _collectionChangedHandler = (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (BibleChapterListViewItemModel item in e.NewItems)
                {
                    SubscribeToChapterEvents(item);
                }
            }

            if (e.OldItems is null) return;
            {
                foreach (BibleChapterListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromChapterEvents(item);
                }
            }
        };
        Chapters.CollectionChanged += _collectionChangedHandler;

        // Subscribe to play service stopped event
        _playService.OnStopped += OnPlayServiceStopped;
    }

    private void SubscribeToChapterEvents(BibleChapterListViewItemModel chapter)
    {
        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "Play" || sender is not BibleChapterListViewItemModel item) return;
            if (item.Play)
            {
                _ = HandlePlayChapter(item);
            }
            else
            {
                _playService.Stop();
            }
        }

        chapter.PropertyChanged += Handler;
        _propertyChangedHandlers[chapter] = Handler;
    }

    private void UnsubscribeFromChapterEvents(BibleChapterListViewItemModel chapter)
    {
        if (!_propertyChangedHandlers.TryGetValue(chapter, out var handler)) return;
        chapter.PropertyChanged -= handler;
        _propertyChangedHandlers.Remove(chapter);
    }

    private async Task HandlePlayChapter(BibleChapterListViewItemModel chapter)
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, async () =>
        {
            if (_currentlyPlaying is not null && _currentlyPlaying != chapter)
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
                    {
                        if (_tentative is null) return;
                        url = await _urlRefreshService.GetBibleChapterUrl(
                            _tentative.LanguageCode,
                            _tentative.PublicationCode,
                            _tentative.BookNumber,
                            chapter.Number,
                            chapter.LookUpPath);
                    }

                    await _playService.Play(url);
                });
            }
            catch
            {
                _currentlyPlaying?.Play = false;
                await _toastService.ShowMessage("Media download failed. Check your internet connection.");
            }
            finally
            {
                _currentlyPlaying?.IsBusy = false;
            }
        }, ex => _logger.Error(ex, "ChapterSelectionViewModel: @lock disposed error."));
    }

    private async void OnPlayServiceStopped()
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(_lock, () =>
            {
                if (_currentlyPlaying is null) return Task.CompletedTask;
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
                return Task.CompletedTask;
            }, ex => _logger.Error(ex, "ChapterSelectionViewModel: @lock disposed error."));
        }
        catch (Exception e)
        {
            _logger.Error(e, "ChapterSelectionViewModel: OnPlayServiceStopped error.");
        }
    }

    private async Task PopulateChapters(string languageCode, string publicationCode, int bookNumber)
    {
        var chapters = await _mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);

        // Build the list of chapter view models
        var chapterViewModelList = new List<BibleChapterListViewItemModel>();
        BibleChapterListViewItemModel? selectedChapter = null;

        foreach (var chapter in chapters.Select(x => x.Value))
        {
            var chapterVm = new BibleChapterListViewItemModel(chapter);

            chapterViewModelList.Add(chapterVm);

            if (_current is null || _tentative is null) continue;

            if (_current.LanguageCode != _tentative.LanguageCode
                || _current.PublicationCode != _tentative.PublicationCode
                || _current.BookNumber != _tentative.BookNumber
                || _current.ChapterNumber != chapter.Number) continue;
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
        _state.StateChanged -= OnBibleReadingInitialized;
        _playService.OnStopped -= OnPlayServiceStopped;
        
        // Unsubscribe from collection changes
        if (Chapters is not null && _collectionChangedHandler is not null)
        {
            Chapters.CollectionChanged -= _collectionChangedHandler;
        }
        
        // Stop any ongoing preview playback when navigating away
        _playService.Stop();
        
        if (Chapters is not null)
        {
            foreach (var chapter in Chapters)
            {
                UnsubscribeFromChapterEvents(chapter);
            }
        }
        
        _propertyChangedHandlers.Clear();

        _lock.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

public partial class BibleChapterListViewItemModel : ObservableObject, IComparable
{
    private readonly BibleChapter _chapter;

    public BibleChapterListViewItemModel(BibleChapter chapter)
    {
        _chapter = chapter;
        TogglePlayCommand = new RelayCommand(() => Play = !Play);
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

    public int CompareTo(object? obj)
    {
        return Number.CompareTo((obj as BibleChapterListViewItemModel)?.Number);
    }
}