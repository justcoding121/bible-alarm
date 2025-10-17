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
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private readonly IContainer container;

        private MediaService mediaService;
        private IToastService toastService;
        private IPreviewPlayService playService;
        private BibleReadingSchedule current;
        private BibleReadingSchedule tentative;
        private INavigationService navigationService;
        private IMediaCacheService cacheService;
        private IDownloadService downloadService;

        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public ChapterSelectionViewModel(IContainer container)
        {
            this.container = container;

            this.mediaService = this.container.Resolve<MediaService>();
            this.toastService = this.container.Resolve<IToastService>();
            this.playService = this.container.Resolve<IPreviewPlayService>();
            this.navigationService = this.container.Resolve<INavigationService>();
            this.downloadService = this.container.Resolve<IDownloadService>();
            this.cacheService = this.container.Resolve<IMediaCacheService>();

            BackCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.GoBack();
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

                tentative.ChapterNumber = x.Number;

                ReduxContainer.Store.Dispatch(new ChapterSelectedAction()
                {
                    CurrentBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        LanguageCode = tentative.LanguageCode,
                        PublicationCode = tentative.PublicationCode,
                        BookNumber = tentative.BookNumber,
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
                       current = x.CurrentBibleReadingSchedule;
                       tentative = x.TentativeBibleReadingSchedule;
                       await initialize(tentative.LanguageCode, tentative.PublicationCode, tentative.BookNumber);
                       IsBusy = false;
                   });


            subscriptions.Add(subscription);
        }

        public ICommand BackCommand { get; set; }
        public ICommand SetChapterCommand { get; set; }

        public BibleChapterListViewItemModel SelectedChapter { get; set; }

        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => this.Set(ref isBusy, value);
        }

        private ObservableCollection<BibleChapterListViewItemModel> chapters;
        public ObservableCollection<BibleChapterListViewItemModel> Chapters
        {
            get => chapters;
            set => this.Set(ref chapters, value);
        }

        private BibleChapterListViewItemModel currentlyPlaying;
        private SemaphoreSlim @lock = new SemaphoreSlim(1);
        private async Task initialize(string languageCode, string publicationCode, int bookNumber)
        {
            await populateChapters(languageCode, publicationCode, bookNumber);


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
                                 await @lock.WaitAsync();

                                 try
                                 {
                                     if (currentlyPlaying != null && currentlyPlaying != y)
                                     {
                                         currentlyPlaying.Play = false;
                                         currentlyPlaying.IsBusy = false;
                                     }

                                     currentlyPlaying = y;

                                     currentlyPlaying.IsBusy = true;

                                     try
                                     {

                                         var url = y.Url;

                                         await Task.Run(async () =>
                                         {
                                             if (!await downloadService.FileExists(url))
                                             {
                                                 url = await cacheService.GetBibleChapterUrl(
                                                                 tentative.LanguageCode,
                                                                 tentative.PublicationCode,
                                                                 tentative.BookNumber,
                                                                 y.Number,
                                                                 y.LookUpPath);
                                             }

                                             await playService.Play(url);
                                         });
                                     }
                                     catch
                                     {
                                         currentlyPlaying.Play = false;
                                         await toastService.ShowMessage("Failed to download the file.");
                                     }

                                     currentlyPlaying.IsBusy = false;

                                 }
                                 finally
                                 {
                                     try
                                     {
                                         @lock.Release();
                                     }
                                     catch (ObjectDisposedException e)
                                     {
                                         logger.Error(e, "ChapterSelectionViewModel: @lock disposed error.");
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
                                    playService.Stop();
                                })
                                .Subscribe();

            var subscription3 = Observable.FromEvent(ev => playService.OnStopped += ev,
                                                    ev => playService.OnStopped -= ev)
                                .Do(async y =>
                                {
                                    await @lock.WaitAsync();

                                    try
                                    {
                                        if (currentlyPlaying != null)
                                        {
                                            currentlyPlaying.Play = false;
                                            currentlyPlaying.IsBusy = false;
                                            currentlyPlaying = null;
                                        }
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            @lock.Release();
                                        }
                                        catch (ObjectDisposedException e)
                                        {
                                            logger.Error(e, "TrackSelectionViewModel: @lock disposed error.");
                                        }
                                    }
                                })
                                .Subscribe();

            subscriptions.AddRange(new[] { subscription1, subscription2, subscription3 });
        }

        private async Task populateChapters(string languageCode, string publicationCode, int bookNumber)
        {
            var chapters = await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber);
            var chapterVMs = new ObservableCollection<BibleChapterListViewItemModel>();

            if (CurrentDevice.RuntimePlatform == Device.WinUI)
            {
                Chapters = chapterVMs;
            }

            foreach (var chapter in chapters.Select(x => x.Value))
            {
                var chapterVM = new BibleChapterListViewItemModel(chapter);

                chapterVMs.Add(chapterVM);

                if (current.LanguageCode == tentative.LanguageCode
                    && current.PublicationCode == tentative.PublicationCode
                    && current.BookNumber == tentative.BookNumber
                    && current.ChapterNumber == chapter.Number)
                {
                    chapterVM.IsSelected = true;
                    SelectedChapter = chapterVM;
                }
            }

            if (CurrentDevice.RuntimePlatform != Device.WinUI)
            {
                Chapters = chapterVMs;
            }
        }

        public void Dispose()
        {
            subscriptions.ForEach(x => x.Dispose());

            mediaService.Dispose();
            toastService.Dispose();
            playService.Dispose();
            downloadService.Dispose();
            cacheService.Dispose();
            @lock.Dispose();
        }
    }

    public class BibleChapterListViewItemModel : ViewModel, IComparable
    {
        private readonly BibleChapter chapter;

        public BibleChapterListViewItemModel(BibleChapter chapter)
        {
            this.chapter = chapter;
            TogglePlayCommand = new Command(() => Play = !Play);
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.Set(ref isSelected, value);
        }

        public ICommand TogglePlayCommand { get; set; }

        public string LookUpPath => chapter.Source.LookUpPath;
        public int Number => chapter.Number;

        public string Title => chapter.Title;
        public string Url => chapter.Source.Url;

        private bool play;
        public bool Play
        {
            get => play;
            set => this.Set(ref play, value);
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => this.Set(ref isBusy, value);
        }

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleChapterListViewItemModel).Number);
        }
    }
}
