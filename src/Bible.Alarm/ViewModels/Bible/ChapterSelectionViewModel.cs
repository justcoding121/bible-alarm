using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;
using Mvvmicro;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels
{
    public class ChapterSelectionViewModel : ViewModel, IDisposable
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;


        private readonly IContainer _container;

        private MediaService _mediaService;
        private IToastService _toastService;
        private IPreviewPlayService _playService;
        private BibleReadingSchedule _current;
        private BibleReadingSchedule _tentative;
        private INavigationService _navigationService;
        private IMediaCacheService _cacheService;
        private IDownloadService _downloadService;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public ChapterSelectionViewModel(IContainer container)
        {
            this._container = container;

            this._mediaService = this._container.Resolve<MediaService>();
            this._toastService = this._container.Resolve<IToastService>();
            this._playService = this._container.Resolve<IPreviewPlayService>();
            this._navigationService = this._container.Resolve<INavigationService>();
            this._downloadService = this._container.Resolve<IDownloadService>();
            this._cacheService = this._container.Resolve<IMediaCacheService>();

            BackCommand = new Command(async () =>
            {
                IsBusy = true;
                await _navigationService.GoBack();
                IsBusy = false;
            });

            SetChapterCommand = new Command<BibleChapterListViewItemModel>(x =>
            {
                IsBusy = true;

                if (SelectedChapter != null)
                {
                    SelectedChapter.IsSelected = false;
                }

                SelectedChapter = x;
                SelectedChapter.IsSelected = true;

                _tentative.ChapterNumber = x.Number;

                ReduxContainer.Store.Dispatch(new ChapterSelectedAction()
                {
                    CurrentBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        LanguageCode = _tentative.LanguageCode,
                        PublicationCode = _tentative.PublicationCode,
                        BookNumber = _tentative.BookNumber,
                        ChapterNumber = x.Number
                    }
                });
                IsBusy = false;
            });

            //set schedules from initial state.
            //this should fire only once 
            var subscription = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
                   .Select(state => new { state.CurrentBibleReadingSchedule, state.TentativeBibleReadingSchedule })
                   .Where(x => x.CurrentBibleReadingSchedule != null && x.TentativeBibleReadingSchedule != null)
                   .DistinctUntilChanged()
                   .Take(1)
                   .Subscribe(async x =>
                   {
                       _current = x.CurrentBibleReadingSchedule;
                       _tentative = x.TentativeBibleReadingSchedule;
                       await Initialize(_tentative.LanguageCode, _tentative.PublicationCode, _tentative.BookNumber);
                       IsBusy = false;
                   });


            _subscriptions.Add(subscription);
        }

        public ICommand BackCommand { get; set; }
        public ICommand SetChapterCommand { get; set; }

        public BibleChapterListViewItemModel SelectedChapter { get; set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.Set(ref _isBusy, value);
        }

        private ObservableCollection<BibleChapterListViewItemModel> _chapters;
        public ObservableCollection<BibleChapterListViewItemModel> Chapters
        {
            get => _chapters;
            set => this.Set(ref _chapters, value);
        }

        private BibleChapterListViewItemModel _currentlyPlaying;
        private SemaphoreSlim _lock = new SemaphoreSlim(1);
        private async Task Initialize(string languageCode, string publicationCode, int bookNumber)
        {
            await PopulateChapters(languageCode, publicationCode, bookNumber);


            var subscription1 = Chapters.Select(added =>
                            {
                                return Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, BibleChapterListViewItemModel>>(
                                   onNextHandler => (object sender, PropertyChangedEventArgs e)
                                                 => onNextHandler(new KeyValuePair<string, BibleChapterListViewItemModel>(e.PropertyName,
                                                                            (BibleChapterListViewItemModel)sender)),
                                                   handler => added.PropertyChanged += handler,
                                                   handler => added.PropertyChanged -= handler)
                                                   .Where(kv => kv.Key == "Play")
                                                   .Select(y => y.Value)
                                                   .Where(y => y.Play);
                            })
                             .Merge()
                             .Do(async y =>
                             {
                                 await _lock.WaitAsync();

                                 try
                                 {
                                     if (_currentlyPlaying != null && _currentlyPlaying != y)
                                     {
                                         _currentlyPlaying.Play = false;
                                         _currentlyPlaying.IsBusy = false;
                                     }

                                     _currentlyPlaying = y;

                                     _currentlyPlaying.IsBusy = true;

                                     try
                                     {

                                         var url = y.Url;

                                         await Task.Run(async () =>
                                         {
                                             if (!await _downloadService.FileExists(url))
                                             {
                                                 url = await _cacheService.GetBibleChapterUrl(
                                                                 _tentative.LanguageCode,
                                                                 _tentative.PublicationCode,
                                                                 _tentative.BookNumber,
                                                                 y.Number,
                                                                 y.LookUpPath);
                                             }

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
                                         Logger.Error(e, "ChapterSelectionViewModel: @lock disposed error.");
                                     }
                                 }

                             })
                             .Subscribe();

            var subscription2 = Chapters.Select(added =>
                                {
                                    return Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, BibleChapterListViewItemModel>>(
                                                   onNextHandler => (object sender, PropertyChangedEventArgs e)
                                                                 => onNextHandler(new KeyValuePair<string, BibleChapterListViewItemModel>(e.PropertyName,
                                                                                            (BibleChapterListViewItemModel)sender)),
                                                                   handler => added.PropertyChanged += handler,
                                                                   handler => added.PropertyChanged -= handler)
                                                                   .Where(kv => kv.Key == "Play")
                                                                   .Select(y => y.Value)
                                                                   .Where(y => !y.Play);
                                })
                                .Merge()
                                .Do(y =>
                                {
                                    _playService.Stop();
                                })
                                .Subscribe();

            var subscription3 = Observable.FromEvent(ev => _playService.OnStopped += ev,
                                                    ev => _playService.OnStopped -= ev)
                                .Do(async y =>
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
                                            Logger.Error(e, "TrackSelectionViewModel: @lock disposed error.");
                                        }
                                    }
                                })
                                .Subscribe();

            _subscriptions.AddRange(new[] { subscription1, subscription2, subscription3 });
        }

        private async Task PopulateChapters(string languageCode, string publicationCode, int bookNumber)
        {
            var chapters = await _mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
            var chapterVMs = new ObservableCollection<BibleChapterListViewItemModel>();

            if (CurrentDevice.RuntimePlatform == Device.WinUI)
            {
                Chapters = chapterVMs;
            }

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

            if (CurrentDevice.RuntimePlatform != Device.WinUI)
            {
                Chapters = chapterVMs;
            }
        }

        public void Dispose()
        {
            _subscriptions.ForEach(x => x.Dispose());

            _mediaService.Dispose();
            _toastService.Dispose();
            _playService.Dispose();
            _downloadService.Dispose();
            _cacheService.Dispose();
            _lock.Dispose();
        }
    }

    public class BibleChapterListViewItemModel : ViewModel, IComparable
    {
        private readonly BibleChapter _chapter;

        public BibleChapterListViewItemModel(BibleChapter chapter)
        {
            this._chapter = chapter;
            TogglePlayCommand = new Command(() => Play = !Play);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => this.Set(ref _isSelected, value);
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
            set => this.Set(ref _play, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.Set(ref _isBusy, value);
        }

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleChapterListViewItemModel).Number);
        }
    }
}
